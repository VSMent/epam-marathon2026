using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Locations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Services;

public class GetGroupSessionsServiceTests
{
    private readonly Mock<IGuestSessionRepository> _mockGuestSessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _mockGroupReservationRepository = new();
    private readonly Mock<IDishRepository> _mockDishRepository = new();
    private readonly Mock<IReservationRepository> _mockReservationRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ITableRepository> _mockTableRepository = new();
    private readonly Mock<IOrderRepository> _mockOrderRepository = new();
    private readonly Mock<ILocationRepository> _mockLocationRepository = new();
    private readonly GuestSessionService _guestSessionService;

    public GetGroupSessionsServiceTests()
    {
        _guestSessionService = new GuestSessionService(
            _mockGroupReservationRepository.Object,
            _mockGuestSessionRepository.Object,
            _mockDishRepository.Object,
            _mockReservationRepository.Object,
            _mockUserRepository.Object,
            _mockTableRepository.Object,
            _mockOrderRepository.Object,
            _mockLocationRepository.Object);
    }

    [Fact]
    public async Task GetAllSessionsAsync_ValidOwnerAndSessions_ReturnsParticipantsAndTotal()
    {
        // Arrange
        var userId = "owner-user-123";
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            TableId = "table-1",
            WaiterId = "waiter-1"
        };

        var ownerUser = new UserType
        {
            Attributes = [new AttributeType { Name = "given_name", Value = "Yevhenii" }]
        };

        var dish1 = new Dish { Id = "d1", Name = "Caesar", Price = 180, State = DishState.Available };
        var dish2 = new Dish { Id = "d2", Name = "Tom Yum", Price = 220, State = DishState.Available };

        var ownerSession = new GuestSession
        {
            Id = "session-owner",
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            IsOwnerSession = true,
            OwnerUserId = userId,
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "d1", Quantity = 1 }]
        };

        var guestSession = new GuestSession
        {
            Id = "session-guest",
            GroupReservationId = groupReservationId,
            GuestName = "Olena",
            IsOwnerSession = false,
            IsSubmitted = false,
            Dishes = [new OrderItem { DishId = "d2", Quantity = 2 }]
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(ownerUser);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([ownerSession, guestSession]);

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([dish1, dish2]);

        // Act
        var result = await _guestSessionService.GetAllSessionsAsync(userId, groupReservationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Participants.Count());
        Assert.Equal(620, result.Total); // 180 + 2*220 = 620

        var ownerParticipant = result.Participants.First(p => p.Type == ParticipantType.Owner);
        Assert.Equal("session-owner", ownerParticipant.Id);
        Assert.Equal("Yevhenii", ownerParticipant.Name);
        Assert.True(ownerParticipant.IsSubmitted);
        Assert.Equal(180, ownerParticipant.Total);
        Assert.Single(ownerParticipant.Items);

        var guestParticipant = result.Participants.First(p => p.Type == ParticipantType.Guest);
        Assert.Equal("session-guest", guestParticipant.Id);
        Assert.Equal("Olena", guestParticipant.Name);
        Assert.False(guestParticipant.IsSubmitted);
        Assert.Equal(440, guestParticipant.Total);
        Assert.Single(guestParticipant.Items);
    }

    [Fact]
    public async Task GetAllSessionsAsync_OwnerHasRegularPreOrder_ShowsOrderItemsForOwner()
    {
        // Arrange: the owner's menu pre-order lives in Orders; the owner's group session is empty.
        var userId = "owner-user-123";
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = groupReservationId,
                ReservationId = reservationId,
                ReservationToken = "token-1",
                Status = GroupReservationStatus.Open
            });

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation { Id = reservationId, CustomerId = userId, TableId = "t", WaiterId = "w" });

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new UserType { Attributes = [] });

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "session-owner",
                    GroupReservationId = groupReservationId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    OwnerUserId = userId,
                    Dishes = []
                }
            ]);

        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(new Order
            {
                OrderId = "order-1",
                CustomerId = userId,
                ReservationId = reservationId,
                ReservationAddress = "addr",
                ReservationTime = "time",
                Status = OrderStatus.SubmittedByCustomer,
                Dishes = [new OrderItem { DishId = "d1", Quantity = 2 }]
            });

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = "d1", Name = "Caesar", Price = 180, State = DishState.Available }]);

        // Act
        var result = await _guestSessionService.GetAllSessionsAsync(userId, groupReservationId);

        // Assert
        var owner = Assert.Single(result.Participants);
        Assert.Equal(ParticipantType.Owner, owner.Type);
        Assert.Single(owner.Items);
        Assert.Equal(2, owner.Items.First().Quantity);
        Assert.Equal(360, owner.Total); // 2 * 180
        Assert.Equal(360, result.Total);
    }

    [Theory]
    [InlineData(OrderStatus.Created, false)]
    [InlineData(OrderStatus.SubmittedByCustomer, true)]
    public async Task GetAllSessionsAsync_OwnerOrder_OwnerSubmittedOnlyWhenOrderIsSubmitted(OrderStatus status, bool expectedSubmitted)
    {
        // Arrange
        var userId = "owner-user-123";
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        SetupGroupWithOwnerSession(userId, groupReservationId, reservationId, GroupReservationStatus.Open, ownerSessionDishes: []);

        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(CreateOrder(userId, reservationId, status, ("d1", 1)));

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = "d1", Name = "Caesar", Price = 180, State = DishState.Available }]);

        // Act
        var result = await _guestSessionService.GetAllSessionsAsync(userId, groupReservationId);

        // Assert
        var owner = Assert.Single(result.Participants);
        Assert.Equal(expectedSubmitted, owner.IsSubmitted);
        Assert.Single(owner.Items);
    }

    [Fact]
    public async Task GetAllSessionsAsync_GroupSubmitted_OwnerShowsOnlyOwnSnapshotNotWholeGroupOrder()
    {
        // Arrange: after submit the Orders entry holds the consolidated group order (owner + guest dishes),
        // but the owner's card must still show only the owner's own dishes from their session snapshot.
        var userId = "owner-user-123";
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        SetupGroupWithOwnerSession(userId, groupReservationId, reservationId, GroupReservationStatus.Submitted,
            ownerSessionDishes: [new OrderItem { DishId = "d1", Quantity = 2 }]);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "session-owner",
                    GroupReservationId = groupReservationId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    OwnerUserId = userId,
                    Dishes = [new OrderItem { DishId = "d1", Quantity = 2 }]
                },
                new GuestSession
                {
                    Id = "session-guest",
                    GroupReservationId = groupReservationId,
                    GuestName = "Olena",
                    IsOwnerSession = false,
                    IsSubmitted = true,
                    Dishes = [new OrderItem { DishId = "d2", Quantity = 4 }]
                }
            ]);

        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(CreateOrder(userId, reservationId, OrderStatus.SubmittedByCustomer, ("d1", 3), ("d2", 4)));

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([
                new Dish { Id = "d1", Name = "Caesar", Price = 180, State = DishState.Available },
                new Dish { Id = "d2", Name = "Tom Yum", Price = 220, State = DishState.Available }
            ]);

        // Act
        var result = await _guestSessionService.GetAllSessionsAsync(userId, groupReservationId);

        // Assert
        var owner = result.Participants.First(p => p.Type == ParticipantType.Owner);
        Assert.True(owner.IsSubmitted);
        var ownerItem = Assert.Single(owner.Items);
        Assert.Equal("d1", ownerItem.Dish.Id);
        Assert.Equal(2, ownerItem.Quantity);
        Assert.Equal(360, owner.Total); // 2 * 180, not the group total
    }

    private void SetupGroupWithOwnerSession(
        string userId,
        string groupReservationId,
        string reservationId,
        GroupReservationStatus groupStatus,
        List<OrderItem> ownerSessionDishes)
    {
        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = groupReservationId,
                ReservationId = reservationId,
                ReservationToken = "token-1",
                Status = groupStatus
            });

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation { Id = reservationId, CustomerId = userId, TableId = "t", WaiterId = "w" });

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new UserType { Attributes = [] });

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "session-owner",
                    GroupReservationId = groupReservationId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    OwnerUserId = userId,
                    Dishes = ownerSessionDishes
                }
            ]);
    }

    private static Order CreateOrder(string userId, string reservationId, OrderStatus status, params (string DishId, int Quantity)[] dishes) => new()
    {
        OrderId = "order-1",
        CustomerId = userId,
        ReservationId = reservationId,
        ReservationAddress = "addr",
        ReservationTime = "time",
        Status = status,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList()
    };

    [Fact]
    public async Task GetAllSessionsAsync_NonOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        var userId = "not-the-owner";
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = "actual-owner",
            TableId = "table-1",
            WaiterId = "waiter-1"
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        // Act & Assert
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _guestSessionService.GetAllSessionsAsync(userId, groupReservationId));
    }

    [Fact]
    public async Task GetAllSessionsAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException()
    {
        // Arrange
        var groupReservationId = "non-existent-group";

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _guestSessionService.GetAllSessionsAsync("some-user", groupReservationId));
    }

    [Fact]
    public async Task GetAllSessionsAsync_ParentReservationNotFound_ThrowsReservationNotFoundException()
    {
        // Arrange
        var groupReservationId = "group-1";
        var reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _guestSessionService.GetAllSessionsAsync("some-user", groupReservationId));
    }
}

