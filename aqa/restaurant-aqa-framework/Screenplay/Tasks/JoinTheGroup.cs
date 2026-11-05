using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>
/// A guest joins the group through the invite and, if a dish is given, adds it to their cart. A dish the scenario has
/// not used yet is created at the fixture location first. The guest is remembered under <paramref name="guestName"/>.
/// </summary>
public sealed class JoinTheGroup(string guestName, string? dishName = null, int quantity = 1) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupPreOrder>();
        var groups = new GroupOrdersApi(actor.UsingTheApi());

        var created = await groups.CreateGuestSessionAsync(group.GroupReservationId, guestName);
        await actor.TheLastResponse().CaptureAsync(created);
        var session = actor.TheLastResponse().Json<GuestSessionCreatedResponse>();
        var guest = new GuestSession(session.SessionId, session.SessionToken);
        group.Guests[guestName] = guest;

        if (dishName is null)
            return;

        var dish = await DishFor(actor, group, dishName);
        var added = await groups.AddDishAsync(guest.SessionId, dish.Id, quantity, guest.SessionToken);
        await actor.TheLastResponse().CaptureAsync(added);
        if (!added.Ok)
            throw new InvalidOperationException(
                $"Adding '{dishName}' for {guestName} failed with {added.Status}: {await added.ReadErrorMessageAsync()}");
    }

    /// <summary>The dish the scenario calls <paramref name="dishName"/>: already remembered, or created now at the fixture
    /// location under the sample prefix so the cleanup sweep also finds it if this scenario is interrupted.</summary>
    private static async Task<GroupDish> DishFor(Actor actor, GroupPreOrder group, string dishName)
    {
        if (group.Dishes.TryGetValue(dishName, out var known))
            return known;

        var fixture = Settings.Current.RequireFixture();
        var dishes = new DishesApi(actor.UsingTheApi());
        var waiter = await actor.AsksForAsync(new TheWaiterSession());

        var request = SampleDataFactory.CreateDishRequest([fixture.LocationId]) with { Name = SampleDataFactory.Prefix + dishName };
        var created = await dishes.CreateDishAsync(request, waiter.IdToken);
        if (!created.Ok)
            throw new InvalidOperationException(
                $"Creating the dish '{dishName}' failed with {created.Status}: {await created.ReadErrorMessageAsync()}");

        var dish = await created.ReadAsAsync<DishResponse>();
        actor.Recall<CleanupScope>().TrackDelete(
            $"dish:{dish.Id}",
            () => dishes.DeleteDishAsync(dish.Id, force: true, bearerToken: waiter.IdToken));

        var entry = new GroupDish(dish.Id, dish.Name, dish.Price, IsAlcohol: false);
        group.Dishes[dishName] = entry;
        return entry;
    }
}
