using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The owner opens the group pre-order screen of the scenario's group and waits until it has loaded.</summary>
public sealed class OpenTheGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupPreOrder>();
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.NavigateAsync(group.ReservationId);
        await screen.WaitUntilLoadedAsync();
    }
}

/// <summary>The owner switches on the "Show Alcohol Only" filter.</summary>
public sealed class ShowOnlyAlcoholicDrinks : ITask
{
    public Task PerformAsAsync(Actor actor) =>
        new GroupOrderPage(actor.LookingAtThePage()).AlcoholFilterButton.ClickAsync();
}

/// <summary>The owner opens the "Plan Logistics" dialog.</summary>
public sealed class PlanTransport : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.PlanLogisticsButton.ClickAsync();
        await screen.LogisticsDialog.WaitForAsync();
    }
}

/// <summary>
/// The owner presses "Send Reminder" on the card of the named guest. The HTTP status of the reminder call is remembered,
/// because the screen can show a success toast whatever the server answered.
/// </summary>
public sealed class SendReminderTo(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var screen = new GroupOrderPage(page);

        var reminderCall = page.WaitForResponseAsync(
            r => r.Request.Method == "POST" && r.Url.Contains("/remind"),
            new() { Timeout = 10000 });
        await screen.ClickReminderFor(guestName);

        int status;
        try
        {
            status = (await reminderCall).Status;
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            // The browser never received an answer to the reminder call (for example, it was blocked). Recorded as 0.
            status = 0;
        }

        actor.Remember(new ReminderDispatch(guestName, status));
    }
}
