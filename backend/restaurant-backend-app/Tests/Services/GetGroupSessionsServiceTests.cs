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
    private readonly GuestSessionService _guestSessionService;

    public GetGroupSessionsServiceTests()
    {
        _guestSessionService = new GuestSessionService(
            _mockGroupReservationRepository.Object,
            _mockGuestSessionRepository.Object,
            _mockDishRepository.Object,
            _mockReservationRepository.Object,
            _mockUserRepository.Object,
            _mockTableRepository.Object);
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

