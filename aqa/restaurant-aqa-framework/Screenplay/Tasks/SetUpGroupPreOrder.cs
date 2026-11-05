using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>
/// Sets up a group pre-order for a new owner: registers and signs in an owner through the service, books the standing
/// fixture table for them, and opens the group. Remembers the owner's account and the group, and schedules the
/// booking's cancellation. The owner's account stays on the environment (there is no route to delete a user).
/// </summary>
public sealed class SetUpGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = Settings.Current.RequireFixture();
        var api = actor.UsingTheApi();
        var auth = new AuthApi(api);

        var registration = SampleDataFactory.RegisterRequest();
        await EnsureSucceeded(await auth.RegisterAsync(registration), "Registering the owner");

        var signIn = await auth.SignInAsync(new SignInRequest(registration.Email, registration.Password));
        await EnsureSucceeded(signIn, "Signing the owner in");
        var ownerToken = (await signIn.ReadAsAsync<SignInResponse>()).IdToken;
        actor.Remember(new OwnerAccount(registration.Email, registration.Password, ownerToken));

        var reservations = new ReservationsApi(api);
        var start = DateTime.UtcNow.Date.AddDays(30 + Random.Shared.Next(0, 600)).AddHours(18);
        var booking = await reservations.CreateReservationAsync(
            new CreateReservationRequest(fixture.TableId, start, start.AddHours(2), GuestCount: 4), ownerToken);
        await EnsureSucceeded(booking, "Booking the fixture table");
        var reservation = await booking.ReadAsAsync<ReservationResponse>();
        actor.Recall<CleanupScope>().TrackDelete(
            $"reservation:{reservation.Id}",
            () => reservations.CancelReservationAsync(reservation.Id, ownerToken));

        var groups = new GroupOrdersApi(api);
        var ownerSession = await (await groups.GetOwnerGuestSessionIdAsync(reservation.Id, ownerToken))
            .ReadAsAsync<OwnerGuestSessionResponse>();
        var inviteToken = await (await groups.GetInviteTokenAsync(reservation.Id, ownerToken))
            .ReadAsAsync<InviteTokenResponse>();
        var invite = await (await groups.GetGroupInviteAsync(inviteToken.Token)).ReadAsAsync<GroupInviteResponse>();

        var group = new GroupPreOrder(reservation.Id, invite.GroupReservationId, ownerSession.GuestSessionId, invite.LocationAddress);
        var alcoholic = await (await new DishesApi(api).GetDishAsync(fixture.AlcoholDishId)).ReadAsAsync<DishResponse>();
        group.Dishes[GroupPreOrder.AlcoholicDishKey] = new GroupDish(alcoholic.Id, alcoholic.Name, alcoholic.Price, IsAlcohol: true);

        actor.Remember(group);
    }

    private static async Task EnsureSucceeded(IAPIResponse response, string whatHappened)
    {
        if (!response.Ok)
            throw new InvalidOperationException($"{whatHappened} failed with {response.Status}: {await response.ReadErrorMessageAsync()}");
    }
}
