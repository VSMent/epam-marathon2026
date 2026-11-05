using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest who has joined the group submits their cart. The outcome is the scenario's last response.</summary>
public sealed class SubmitTheGuestCart(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupPreOrder>();
        if (!group.Guests.TryGetValue(guestName, out var guest))
            throw new InvalidOperationException($"{guestName} has not joined the group in this scenario.");

        var submitted = await new GroupOrdersApi(actor.UsingTheApi()).SubmitGuestCartAsync(guest.SessionId, guest.SessionToken);
        await actor.TheLastResponse().CaptureAsync(submitted);
        if (!submitted.Ok)
            throw new InvalidOperationException(
                $"Submitting the cart of {guestName} failed with {submitted.Status}: {await submitted.ReadErrorMessageAsync()}");
    }
}
