using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Mappers;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Locations;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using APIHandler.Services.Notifications;

namespace APIHandler.Services.GuestSessions;

public class GuestSessionService(
    IGroupReservationRepository groupReservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IDishRepository dishRepository,
    IReservationRepository reservationRepository,
    IUserRepository userRepository,
    ITableRepository tableRepository,
    IOrderRepository orderRepository,
    ILocationRepository locationRepository) : IGuestSessionService
{
    public async Task<bool> IsAuthorizedAsync(string guestSessionToken, string guestSessionId)
    {
        string hash = Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(guestSessionToken)));
        GuestSession? guestSession = await guestSessionRepository.GetByTokenHash(hash);
        if (guestSession is null)
            return false;

        return guestSession.Id == guestSessionId;
    }

    public async Task<CreatGuestSessionResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request)
    {
        GroupReservation group = await GetGroupReservationAndValidateAsync(request.GroupReservationId);

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(group.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(group.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new GroupReservationExpiredException($"Associated reservation for group '{request.GroupReservationId}' not found.");

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw new GuestSessionExpiredException($"This pre-order session has expired or is no longer active. Current reservation status: '{reservation.Status}'.");
        }

        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException($"Guest limit exceeded. Maximum capacity is {reservation.GuestCount}, but {joinedGuestsCount} guests have already joined.");

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);

        var session = new GuestSession()
        {
            Id = Guid.NewGuid().ToString(),
            GuestName = request.GuestName,
            GroupReservationId = group.GroupReservationId,
            SessionTokenHash = Convert.ToBase64String(SHA256.HashData(tokenBytes)),
            Dishes = [],
        };

        await guestSessionRepository.SaveAsync(session);

        return new CreatGuestSessionResponse(session.Id, Convert.ToBase64String(tokenBytes));
    }

    public async Task<GuestMenuResponse> GetGuestMenuAsync(string guestSessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new ValidationException($"Guest session '{guestSessionId}' not found.");

        await ValidateGroupReservationId(session.GroupReservationId);

        // Guests see the same full menu as the reservation owner (the regular /dishes list).
        var dishes = await dishRepository.GetAllDishesAsync();

        var dishesMapped = dishes
            .GroupBy(d => d.DishType)
            .ToDictionary(k => k.Key, GuestMenuDishMapper.Map);

        return new GuestMenuResponse(dishesMapped);
    }

    public async Task AddDishAsync(string guestSessionId, string dishId, int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException($"Quantity ({quantity}) must be greater than zero.");

        GuestSession session = await GetGuestSessionAsync(guestSessionId);

        var dish = await dishRepository.GetDishByIdAsync(dishId)
            ?? throw new ValidationException($"Dish '{dishId}' does not exist.");

        if (dish.State != DishState.Available)
            throw new DishIsNotAvailableException($"Dish '{dishId}' is currently unavailable. State: '{dish.State}'.");

        session.Dishes ??= [];

        var existingDish = session.Dishes.FirstOrDefault(d => d.DishId == dishId);

        if (existingDish != null)
        {
            existingDish.Quantity += quantity;
        }
        else
        {
            session.Dishes.Add(new OrderItem
            {
                DishId = dishId,
                Quantity = quantity,
                State = OrderItemState.Pending
            });
        }

        await guestSessionRepository.SaveAsync(session);
    }

    public async Task RemoveDishAsync(string guestSessionId, string dishId, int quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        GuestSession session = await GetGuestSessionAsync(guestSessionId);

        if (session.Dishes == null || session.Dishes.Count == 0)
            throw new DishNotFoundException($"Dish {dishId} not found in guest session.");

        var existingDish = session.Dishes.FirstOrDefault(d => d.DishId == dishId)
            ?? throw new DishNotFoundException($"Dish {dishId} not found in guest session.");

        existingDish.Quantity -= quantity;

        if (existingDish.Quantity <= 0)
        {
            session.Dishes.Remove(existingDish);
        }

        await guestSessionRepository.SaveAsync(session);
    }

    public async Task<GroupReservationStatusResponse> GetGroupReservationStatusAsync(string groupReservationId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId)
            ?? throw new InvalidOperationException("Associated reservation not found.");

        int currentParticipants = await GetCurrentParticipantCountAsync(groupReservationId);

        return new GroupReservationStatusResponse
        {
            CurrentParticipants = currentParticipants,
            MaxCapacity = reservation.GuestCount,
            Status = group.Status
        };
    }

    
    public async Task<GetAllSessionsResponse> DeleteGuestSessionAsync(string userId, string groupReservationId, string guestSessionId)
    {
        await ValidateReservationOwnerAsync(groupReservationId, userId);

        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException($"Guest session with id {guestSessionId} not found.");

        if (session.GroupReservationId != groupReservationId)
        {
            throw new ValidationException("Guest session does not belong to this group reservation.");
        }

        if (session.IsOwnerSession)
        {
            throw new ValidationException("Cannot remove the owner session from the group reservation.");
        }

        await guestSessionRepository.DeleteAsync(session);

        return await BuildAllSessionsResponseAsync(groupReservationId);
    }

    public async Task<GetAllSessionsResponse> GetAllSessionsAsync(string userId, string groupReservationId)
    {
        await ValidateReservationOwnerAsync(groupReservationId, userId);
        return await BuildAllSessionsResponseAsync(groupReservationId);
    }

    private async Task<GetAllSessionsResponse> BuildAllSessionsResponseAsync(string groupReservationId)
    {
        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);

        // The owner's own items are saved as their regular pre-order (Orders table) from the menu.
        // When that order exists it is the source of truth for the owner; otherwise use the session dishes.
        var currentGroup = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        var ownerOrder = await GetOwnerOrderAsync(currentGroup);
        bool groupSubmitted = currentGroup?.Status == GroupReservationStatus.Submitted;

        var dishIds = sessions
            .SelectMany(s => s.Dishes)
            .Select(s => s.DishId)
            .Concat(ownerOrder?.Dishes.Select(d => d.DishId) ?? Enumerable.Empty<string>())
            .Distinct()
            .ToArray();

        var allDishes = await dishRepository.GetDishesByIdsAsync(dishIds);
        var dishesById = allDishes.ToDictionary(d => d.Id);

        var participants = new List<Participant>();

        foreach (var guestSession in sessions)
        {
            // Before the group is submitted, the owner's items come from their regular pre-order.
            // After it is submitted, the Orders entry holds the consolidated group order, so the owner
            // shows the snapshot of their own items saved on their session at submit time.
            bool useOwnerOrder = guestSession.IsOwnerSession && !groupSubmitted && ownerOrder is not null;
            var sourceDishes = useOwnerOrder ? ownerOrder!.Dishes : guestSession.Dishes;

            var items = sourceDishes
                .Where(d => dishesById.ContainsKey(d.DishId))
                .Select(d => new OrderItemResponse
                {
                    Dish = DishMapper.ToDishResponse(dishesById[d.DishId]),
                    Quantity = d.Quantity
                }).ToArray();

            decimal total = items.Sum(i => i.Quantity * i.Dish.Price);
            var type = guestSession.IsOwnerSession ? ParticipantType.Owner : ParticipantType.Guest;

            string name = guestSession.GuestName;
            if (guestSession.IsOwnerSession)
            {
                try
                {
                    var groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(guestSession.GroupReservationId);
                    if (groupReservation?.ReservationId != null)
                    {
                        var reservation = await reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
                        if (!string.IsNullOrEmpty(reservation?.CustomerId))
                        {
                            var user = await userRepository.GetUserByIdAsync(reservation.CustomerId);
                            var givenName = user?.Attributes?.FirstOrDefault(a => a.Name == "given_name" || a.Name == "name")?.Value;
                            if (!string.IsNullOrEmpty(givenName))
                            {
                                name = givenName;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to guestSession.GuestName
                }
            }

            // The owner counts as submitted once their own pre-order is submitted (or the group was sent).
            bool isSubmitted = guestSession.IsSubmitted
                || (useOwnerOrder && IsOrderSubmitted(ownerOrder!))
                || (guestSession.IsOwnerSession && groupSubmitted);

            var participant = new Participant(guestSession.Id, type, name, items, total, isSubmitted);
            participants.Add(participant);
        }

        var orderedParticipants = participants.OrderByDescending(p => p.Type == ParticipantType.Owner).ToList();
        return new GetAllSessionsResponse(orderedParticipants, orderedParticipants.Sum(p => p.Total));
    }

    private async Task<Order?> GetOwnerOrderAsync(GroupReservation? groupReservation)
    {
        if (string.IsNullOrEmpty(groupReservation?.ReservationId))
        {
            return null;
        }

        return await orderRepository.GetOrderByReservationIdAsync(groupReservation.ReservationId);
    }

    private static bool IsOrderSubmitted(Order order) =>
        order.Status is OrderStatus.SubmittedByCustomer or OrderStatus.Submitted;

    public async Task<string> GetOwnerGuestSessionIdAsync(string userId, string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} not found.");

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
        }

        var groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation == null)
        {
            groupReservation = new GroupReservation
            {
                GroupReservationId = Guid.NewGuid().ToString(),
                ReservationId = reservationId,
                ReservationToken = Guid.NewGuid().ToString("N"),
                Status = GroupReservationStatus.Open
            };
            await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        }

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId);
        var ownerSession = sessions.FirstOrDefault(s => s.IsOwnerSession);
        if (ownerSession == null)
        {
            ownerSession = new GuestSession
            {
                Id = Guid.NewGuid().ToString(),
                GroupReservationId = groupReservation.GroupReservationId,
                GuestName = "Owner",
                Dishes = [],
                IsOwnerSession = true,
                OwnerUserId = userId
            };
            await guestSessionRepository.SaveAsync(ownerSession);
        }

        return ownerSession.Id;
    }

    public async Task SubmitGroupPreOrderAsync(string userId, string groupReservationId, string? reservationId, ILambdaLogger logger)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        if (group.Status == GroupReservationStatus.Closed)
        {
            throw new GroupReservationExpiredException();
        }

        if (group.Status == GroupReservationStatus.Submitted)
        {
            throw new GroupReservationSubmittedException();
        }

        var resId = reservationId ?? group.ReservationId;
        var reservation = await reservationRepository.GetReservationByIdAsync(resId)
            ?? throw new ReservationNotFoundException("Associated reservation not found.");

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ActionForbiddenException("Only the reservation owner can submit the group pre-order.");
        }

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);
        var ownerSession = sessions.FirstOrDefault(s => s.IsOwnerSession);
        var ownerOrder = await orderRepository.GetOrderByReservationIdAsync(resId);

        // The owner's own items live in their regular pre-order. They must be submitted first,
        // otherwise they would be left out of the group order sent to the restaurant.
        List<OrderItem> ownerDishes = [];
        if (ownerOrder is { Dishes.Count: > 0 })
        {
            if (!IsOrderSubmitted(ownerOrder))
            {
                throw new ValidationException("Submit your own pre-order before submitting the group pre-order.");
            }

            ownerDishes = ownerOrder.Dishes;
        }
        else if (ownerSession is { IsSubmitted: true })
        {
            ownerDishes = ownerSession.Dishes;
        }

        var guestDishes = sessions
            .Where(s => !s.IsOwnerSession && s.IsSubmitted)
            .SelectMany(s => s.Dishes);

        var allDishes = ownerDishes.Concat(guestDishes).ToList();

        if (!allDishes.Any())
        {
            throw new ValidationException("No submitted pre-orders found to consolidate.");
        }

        var consolidatedDishes = allDishes
            .GroupBy(d => d.DishId)
            .Select(g => new OrderItem
            {
                DishId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                State = OrderItemState.Pending
            })
            .ToList();

        var existingOrder = await orderRepository.GetOrderByReservationIdAsync(resId);
        if (existingOrder != null)
        {
            existingOrder.Dishes = consolidatedDishes;
            existingOrder.Status = OrderStatus.SubmittedByCustomer;
            await orderRepository.SaveOrderAsync(existingOrder);
        }
        else
        {
            var table = await tableRepository.GetTableAsync(reservation.TableId);
            var location = table != null ? await locationRepository.GetLocationByIdAsync(table.LocationId) : null;
            var address = $"{location?.Address ?? "Restaurant"}, Table {table?.TableNumber.ToString() ?? "1"}";
            var time = $"{reservation.StartTime:MMM dd, yyyy, h:mm tt} - {reservation.EndTime:h:mm tt}";

            var newOrder = new Order
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerId = userId,
                ReservationId = resId,
                ReservationAddress = address,
                ReservationTime = time,
                Dishes = consolidatedDishes,
                Status = OrderStatus.SubmittedByCustomer
            };
            await orderRepository.SaveOrderAsync(newOrder);
        }

        if (ownerSession != null)
        {
            // Keep the owner's own items on their session. The Orders entry now holds the whole group
            // order, so the group view needs this snapshot to show only the owner's own dishes.
            ownerSession.Dishes = ownerDishes
                .Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity, State = d.State })
                .ToList();
            ownerSession.IsSubmitted = true;
            await guestSessionRepository.SaveAsync(ownerSession);
        }

        group.Status = GroupReservationStatus.Submitted;
        await groupReservationRepository.SaveGroupReservationAsync(group);

        logger?.LogLine($"Group pre-order for group {groupReservationId} (reservation {resId}) submitted successfully.");
    }

    public async Task SubmitCartAsync(string guestSessionId)
    {
        GuestSession guestSession = await GetGuestSessionAsync(guestSessionId);

        if (!guestSession.Dishes.Any())
            throw new CartIsEmptyException();

        await ValidateDishesAsync(guestSession.Dishes);

        guestSession.IsSubmitted = true;
        await guestSessionRepository.SaveAsync(guestSession);
    }

    private async Task<GroupReservation> GetGroupReservationAndValidateAsync(string groupReservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        ValidateGroupReservation(group);
        return group;
    }

    private async Task<int> GetCurrentParticipantCountAsync(string groupReservationId)
    {
        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);

        // Owner is always 1 active participant. 
        // If sessions is null/empty, it safely evaluates to 0 joined guests + 1 owner = 1.
        return (sessions?.Count ?? 0) + 1;
    }

    private async Task ValidateGroupReservationId(string groupReservationId) => await GetGroupReservationAndValidateAsync(groupReservationId);

    private static void ValidateGroupReservation([NotNull] GroupReservation? group)
    {
        if (group is null)
            throw new GroupReservationNotFoundException();

        if (group.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();
    }

    private async Task<GuestSession> GetGuestSessionAsync(string sessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(sessionId)
            ?? throw new GuestSessionNotFoundException($"Guest session {sessionId} not found.");

        if (session.IsSubmitted)
            throw new GuestSessionSubmittedException();

        await ValidateGroupReservationId(session.GroupReservationId);

        return session;
    }

    private async Task ValidateDishesAsync(IEnumerable<OrderItem> orders)
    {
        IEnumerable<string> dishIds = orders.Select(o => o.DishId).ToArray();
        IEnumerable<Dish> dishes = await dishRepository.GetDishesByIdsAsync(dishIds);

        if (dishes.FirstOrDefault(d => d.State != DishState.Available) is Dish dish)
            throw new DishIsNotAvailableException($"The dish '{dish.Name}' ({dish.Id}) is not available.");
    }

    private async Task<string> GetLocationIdForGroupAsync(string groupReservationId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId)
            ?? throw new ReservationNotFoundException("Parent reservation not found.");

        var table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new ValidationException("Associated table not found.");

        return table.LocationId;
    }

    
    private async Task ValidateReservationOwnerAsync(string groupReservationId, string userId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(group.ReservationId)
            ?? throw new ReservationNotFoundException("Parent reservation not found.");

        if (reservation.CustomerId != userId)
        {
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
        }
    }

    public async Task CreateGroupOwnerSessionAsync(string userId, string groupReservationId)
    {
        var session = new GuestSession()
        {
            Id = Guid.NewGuid().ToString(),
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            Dishes = [],
            IsOwnerSession = true,
            OwnerUserId = userId,
        };

        await guestSessionRepository.SaveAsync(session);
    }

    public async Task<GetGuestSessionResponse> GetGuestSessionInfoAsync(string guestSessionId)
    {
        var session = await guestSessionRepository.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException("Guest session was not found.");

        var dishIds = session.Dishes
            .Select(s => s.DishId)
            .Distinct()
            .ToArray();

        var allDishes = await dishRepository.GetDishesByIdsAsync(dishIds);
        var dishesById = allDishes.ToDictionary(d => d.Id);

        var items = session.Dishes.Select(d => new OrderItemResponse
        {
            Dish = DishMapper.ToDishResponse(dishesById[d.DishId]),
            Quantity = d.Quantity
        }).ToArray();

        decimal total = items.Sum(i => i.Quantity * i.Dish.Price);

        string name = session.GuestName;
        if (session.IsOwnerSession)
        {
            var groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(session.GroupReservationId);
            var reservation = await reservationRepository.GetReservationByIdAsync(groupReservation!.ReservationId);
            var user = await userRepository.GetUserByIdAsync(reservation!.CustomerId!);
            name = user!.Attributes.First(a => a.Name == "given_name").Value;
        }

        return new GetGuestSessionResponse(name, items, total, session.IsSubmitted);
    }

}




