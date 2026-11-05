using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Locations;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Services;

public class Level2And3GroupSessionsServiceTests
{
    private readonly Mock<IGuestSessionRepository> _mockGuestSessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _mockGroupReservationRepository = new();
    private readonly Mock<IDishRepository> _mockDishRepository = new();
    private readonly Mock<IReservationRepository> _mockReservationRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<ITableRepository> _mockTableRepository = new();
    private readonly Mock<IOrderRepository> _mockOrderRepository = new();
    private readonly Mock<ILocationRepository> _mockLocationRepository = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GuestSessionService _guestSessionService;

    public Level2And3GroupSessionsServiceTests()
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

    #region Level 2: GetOwnerGuestSessionIdAsync Tests

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ExistingOwnerSession_ReturnsExistingSessionId()
    {
        // Arrange
        var userId = "owner-123";
        var reservationId = "res-1";
        var groupReservationId = "grp-1";

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var ownerSession = new GuestSession
        {
            Id = "session-owner-1",
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            IsOwnerSession = true,
            OwnerUserId = userId,
            Dishes = []
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGroupReservationRepository
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync(groupReservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([ownerSession]);

        // Act
        var result = await _guestSessionService.GetOwnerGuestSessionIdAsync(userId, reservationId);

        // Assert
        Assert.Equal("session-owner-1", result);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoGroupOrSession_CreatesGroupAndOwnerSession()
    {
        // Arrange
        var userId = "owner-123";
        var reservationId = "res-1";

        var reservation = new Reservation
        {
            Id = reservationId,
            CustomerId = userId,
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGroupReservationRepository
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        GuestSession? createdSession = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => createdSession = s);

        // Act
        var result = await _guestSessionService.GetOwnerGuestSessionIdAsync(userId, reservationId);

        // Assert
        Assert.NotEmpty(result);
        Assert.NotNull(createdSession);
        Assert.True(createdSession.IsOwnerSession);
        Assert.Equal(userId, createdSession.OwnerUserId);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NonOwner_ThrowsActionForbiddenException()
    {
        // Arrange
        var reservation = new Reservation
        {
            Id = "res-1",
            CustomerId = "actual-owner",
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync("res-1"))
            .ReturnsAsync(reservation);

        // Act & Assert
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _guestSessionService.GetOwnerGuestSessionIdAsync("intruder", "res-1"));
    }

    #endregion

    #region Level 2: SubmitGroupPreOrderAsync Tests

    [Fact]
    public async Task SubmitGroupPreOrderAsync_SubmittedSessions_AggregatesDishesAndSavesOrder()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
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
            TableId = "tbl-1",
            WaiterId = "wtr-1",
            StartTime = System.DateTime.UtcNow.AddDays(1),
            EndTime = System.DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var session1 = new GuestSession
        {
            Id = "s1",
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            IsOwnerSession = true,
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "d1", Quantity = 2 }]
        };

        var session2 = new GuestSession
        {
            Id = "s2",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsOwnerSession = false,
            IsSubmitted = true,
            Dishes =
            [
                new OrderItem { DishId = "d1", Quantity = 1 }, // same dish d1 (2 + 1 = 3)
                new OrderItem { DishId = "d2", Quantity = 4 }
            ]
        };

        var unsubmittedSession = new GuestSession
        {
            Id = "s3",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 2",
            IsOwnerSession = false,
            IsSubmitted = false,
            Dishes = [new OrderItem { DishId = "d3", Quantity = 10 }] // should NOT be included
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([session1, session2, unsubmittedSession]);

        Order? savedOrder = null;
        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync((Order?)null);

        _mockOrderRepository
            .Setup(r => r.SaveOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o);

        // Act
        await _guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId, _mockLogger.Object);

        // Assert
        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.SubmittedByCustomer, savedOrder.Status);
        Assert.Equal(2, savedOrder.Dishes.Count); // d1 and d2 only

        var d1Item = savedOrder.Dishes.First(d => d.DishId == "d1");
        Assert.Equal(3, d1Item.Quantity); // 2 + 1

        var d2Item = savedOrder.Dishes.First(d => d.DishId == "d2");
        Assert.Equal(4, d2Item.Quantity);

        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_NoSubmittedDishes_ThrowsValidationException()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
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
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([]);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId, _mockLogger.Object));
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_OwnerOrderNotSubmitted_ThrowsValidationExceptionAndDoesNotSubmitGroup()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
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
            .ReturnsAsync(new Reservation { Id = reservationId, CustomerId = userId, TableId = "tbl-1", WaiterId = "wtr-1" });

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([CreateOwnerSession(groupReservationId)]);

        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(CreateOrder(userId, reservationId, OrderStatus.Created, ("d1", 2)));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId, _mockLogger.Object));

        Assert.Equal(GroupReservationStatus.Open, groupReservation.Status);
        _mockGroupReservationRepository.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrderAsync_OwnerOrderSubmitted_MergesOwnerAndGuestDishesWithoutLosingOwnerItems()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
        var reservationId = "res-1";

        var groupReservation = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = reservationId,
            ReservationToken = "token-1",
            Status = GroupReservationStatus.Open
        };

        var ownerSession = CreateOwnerSession(groupReservationId);

        var guestSession = new GuestSession
        {
            Id = "s-guest",
            GroupReservationId = groupReservationId,
            GuestName = "Guest 1",
            IsOwnerSession = false,
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "d1", Quantity = 1 }, new OrderItem { DishId = "d2", Quantity = 4 }]
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(new Reservation { Id = reservationId, CustomerId = userId, TableId = "tbl-1", WaiterId = "wtr-1" });

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([ownerSession, guestSession]);

        _mockOrderRepository
            .Setup(r => r.GetOrderByReservationIdAsync(reservationId))
            .ReturnsAsync(CreateOrder(userId, reservationId, OrderStatus.SubmittedByCustomer, ("d1", 2)));

        Order? savedOrder = null;
        _mockOrderRepository
            .Setup(r => r.SaveOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o);

        GuestSession? savedOwnerSession = null;
        _mockGuestSessionRepository
            .Setup(r => r.SaveAsync(It.Is<GuestSession>(s => s.IsOwnerSession)))
            .Callback<GuestSession>(s => savedOwnerSession = s);

        // Act
        await _guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId, _mockLogger.Object);

        // Assert
        Assert.NotNull(savedOrder);
        Assert.Equal(OrderStatus.SubmittedByCustomer, savedOrder.Status);
        Assert.Equal(2, savedOrder.Dishes.Count);
        Assert.Equal(3, savedOrder.Dishes.First(d => d.DishId == "d1").Quantity); // owner 2 + guest 1
        Assert.Equal(4, savedOrder.Dishes.First(d => d.DishId == "d2").Quantity); // guest 4

        // The owner's own items are kept on their session so the group view still shows only their dishes.
        Assert.NotNull(savedOwnerSession);
        Assert.True(savedOwnerSession.IsSubmitted);
        var ownerItem = Assert.Single(savedOwnerSession.Dishes);
        Assert.Equal("d1", ownerItem.DishId);
        Assert.Equal(2, ownerItem.Quantity);

        Assert.Equal(GroupReservationStatus.Submitted, groupReservation.Status);
    }

    private static GuestSession CreateOwnerSession(string groupReservationId) => new()
    {
        Id = "s-owner",
        GroupReservationId = groupReservationId,
        GuestName = "Owner",
        IsOwnerSession = true,
        Dishes = []
    };

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

    #endregion

    #region Level 3: DeleteGuestSessionAsync Tests

    [Fact]
    public async Task DeleteGuestSessionAsync_ValidGuestSession_DeletesAndReturnsUpdatedList()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
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
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        var guestSession = new GuestSession
        {
            Id = "session-to-delete",
            GroupReservationId = groupReservationId,
            GuestName = "Guest",
            IsOwnerSession = false,
            Dishes = []
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId("session-to-delete"))
            .ReturnsAsync(guestSession);

        _mockGuestSessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservationId))
            .ReturnsAsync([]);

        _mockDishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);

        // Act
        var result = await _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, "session-to-delete");

        // Assert
        _mockGuestSessionRepository.Verify(r => r.DeleteAsync(guestSession), Times.Once);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_CannotDeleteOwnerSession_ThrowsValidationException()
    {
        // Arrange
        var userId = "owner-123";
        var groupReservationId = "grp-1";
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
            TableId = "tbl-1",
            WaiterId = "wtr-1"
        };

        var ownerSession = new GuestSession
        {
            Id = "session-owner",
            GroupReservationId = groupReservationId,
            GuestName = "Owner",
            IsOwnerSession = true,
            Dishes = []
        };

        _mockGroupReservationRepository
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync(groupReservation);

        _mockReservationRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync(reservation);

        _mockGuestSessionRepository
            .Setup(r => r.GetByGuestSessionId("session-owner"))
            .ReturnsAsync(ownerSession);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, "session-owner"));
    }

    #endregion
}

