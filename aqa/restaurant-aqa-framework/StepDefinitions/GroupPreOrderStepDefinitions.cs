using System.Globalization;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>Steps for the group pre-order: the owner's screen, the guests' carts, and the reminders.</summary>
[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("the owner has a group pre-order at the fixture restaurant")]
    public Task GivenOwnerHasAGroupPreOrder() => Actor.AttemptsToAsync(new SetUpGroupPreOrder());

    [Given("the owner has signed in")]
    public Task GivenOwnerHasSignedIn()
    {
        var owner = Recall<OwnerAccount>();
        return Actor.AttemptsToAsync(new SignInThroughTheLoginForm(owner.Email, owner.Password));
    }

    [Given("guest {string} has joined the group")]
    public Task GivenGuestHasJoined(string guest) => Actor.AttemptsToAsync(new JoinTheGroup(guest));

    [Given("guest {string} has ordered {string}")]
    public Task GivenGuestHasOrdered(string guest, string dish) => Actor.AttemptsToAsync(new JoinTheGroup(guest, dish));

    [Given("guest {string} has ordered the alcoholic drink")]
    public Task GivenGuestHasOrderedTheAlcoholicDrink(string guest) =>
        Actor.AttemptsToAsync(new JoinTheGroup(guest, GroupPreOrder.AlcoholicDishKey));

    [When("the owner opens the group pre-order")]
    public Task WhenOwnerOpensTheGroupPreOrder() => Actor.AttemptsToAsync(new OpenTheGroupPreOrder());

    [When("the owner turns on the alcohol filter")]
    public Task WhenOwnerTurnsOnTheAlcoholFilter() => Actor.AttemptsToAsync(new ShowOnlyAlcoholicDrinks());

    [When("the owner plans transport")]
    public Task WhenOwnerPlansTransport() => Actor.AttemptsToAsync(new PlanTransport());

    [When("the owner sends a reminder to {string}")]
    public Task WhenOwnerSendsAReminderTo(string guest) => Actor.AttemptsToAsync(new SendReminderTo(guest));

    [Then("a bulk option for {string} is offered")]
    public async Task ThenABulkOptionIsOffered(string dish)
    {
        var suggestion = await Actor.AsksForAsync(new TheBulkSuggestionText());
        await Assert.That(suggestion).IsNotEmpty().Because("the screen should offer a bulk option for the repeated item");
        await Assert.That(suggestion).Contains(dish);
    }

    [Then("no bulk option is offered")]
    public async Task ThenNoBulkOptionIsOffered()
    {
        var suggestion = await Actor.AsksForAsync(new TheBulkSuggestionText());
        await Assert.That(suggestion).IsEmpty().Because($"no optimisation hint should show, but the screen said: {suggestion}");
    }

    [Then("guest {string} is marked as having alcohol")]
    public async Task ThenGuestIsMarkedAsHavingAlcohol(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.MarkedAlcoholic).IsTrue().Because($"{guest}'s card should carry the alcohol chip");
        await Assert.That(card.Lines.Any(line => line.IsAlcoholic)).IsTrue()
            .Because($"{guest}'s alcoholic drink should carry the alcohol marker");
    }

    [Then("guest {string} is not marked as having alcohol")]
    public async Task ThenGuestIsNotMarkedAsHavingAlcohol(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.MarkedAlcoholic).IsFalse().Because($"{guest}'s card should not carry the alcohol chip");
        await Assert.That(card.Lines.Any(line => line.IsAlcoholic)).IsFalse();
    }

    [Then("only alcoholic drinks are listed under their guests")]
    public async Task ThenOnlyAlcoholicDrinksAreListed()
    {
        var cards = await Actor.AsksForAsync(new TheGuestCards());
        var listed = cards.SelectMany(card => card.Lines).ToList();

        await Assert.That(listed).IsNotEmpty().Because("the alcoholic drink should still be listed");
        await Assert.That(listed.All(line => line.IsAlcoholic)).IsTrue()
            .Because("with the alcohol filter on, only alcoholic drinks should be listed");
    }

    [Then("guest {string} has no alcoholic drinks to show")]
    public async Task ThenGuestHasNoAlcoholicDrinksToShow(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.NoAlcoholicDrinksNoticeShown).IsTrue()
            .Because($"{guest} has no alcoholic drink, so the card should say so");
    }

    [Then("ride-sharing partners are offered for the restaurant address")]
    public async Task ThenRideSharingPartnersAreOffered()
    {
        var options = await Actor.AsksForAsync(new TheLogisticsOptions());
        var group = Recall<GroupPreOrder>();

        await Assert.That(options.VenueAddress).IsEqualTo(group.LocationAddress)
            .Because("the destination should be pre-filled with the restaurant address");
        await Assert.That(options.Partners).Contains("Uber");
        await Assert.That(options.Partners).Contains("Bolt");
        await Assert.That(options.Partners).Contains("Uklon");
    }

    [Then("guest {string} is listed with the dish {string}")]
    public async Task ThenGuestIsListedWithTheDish(string guest, string dish)
    {
        var card = await CardOf(guest);
        // The screen shows the dish under the sample-prefixed name the scenario created it with.
        await Assert.That(card.Lines.Any(line => line.DishName.Contains(dish))).IsTrue()
            .Because($"{guest}'s card should list the dish {dish}");
    }

    [Then("the subtotal of guest {string} is the price of {string}")]
    public async Task ThenSubtotalIsThePriceOfTheDish(string guest, string dish)
    {
        var card = await CardOf(guest);
        var price = Recall<GroupPreOrder>().Dishes[dish].Price.ToString("0.##", CultureInfo.InvariantCulture);
        await Assert.That(card.Subtotal).Contains(price)
            .Because($"{guest}'s subtotal should be the price of {dish}, but it reads: {card.Subtotal}");
    }

    [Then("guest {string} is flagged as having no dishes yet")]
    public async Task ThenGuestIsFlaggedAsHavingNoDishes(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.NoDishesWarningShown).IsTrue().Because($"{guest} has not chosen a dish yet");
        await Assert.That(card.Lines).IsEmpty();
    }

    [Then("guest {string} is not flagged as having no dishes yet")]
    public async Task ThenGuestIsNotFlaggedAsHavingNoDishes(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.NoDishesWarningShown).IsFalse().Because($"{guest} has chosen a dish");
    }

    [Then("guest {string} can be sent a reminder")]
    public async Task ThenGuestCanBeSentAReminder(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.ReminderLabel).Contains("Send Reminder")
            .Because($"{guest} has no dishes, so the screen should offer a reminder");
    }

    [Then("the reminder to {string} is delivered")]
    public async Task ThenTheReminderIsDelivered(string guest)
    {
        var dispatch = Recall<ReminderDispatch>();
        await Assert.That(dispatch.GuestName).IsEqualTo(guest);
        await Assert.That(dispatch.Status >= 200 && dispatch.Status < 300).IsTrue()
            .Because($"the reminder call for {guest} was answered with HTTP {dispatch.Status} (0 means no answer reached the browser)");
    }

    [Then("a confirmation toast says {string}")]
    public async Task ThenAConfirmationToastSays(string message)
    {
        var toasts = await Actor.AsksForAsync(new TheSuccessToasts());
        await Assert.That(toasts).Contains(message);
    }

    [Then("the reminder to {string} is marked as sent")]
    public async Task ThenTheReminderIsMarkedAsSent(string guest)
    {
        var card = await CardOf(guest);
        await Assert.That(card.ReminderLabel).Contains("Reminder Sent")
            .Because($"the reminder button for {guest} should read as sent");
    }

    private async Task<GuestCard> CardOf(string guest)
    {
        var cards = await Actor.AsksForAsync(new TheGuestCards());
        return cards.FirstOrDefault(card => card.Name.Contains(guest))
            ?? throw new InvalidOperationException($"The screen has no card for {guest}. Cards: {string.Join(", ", cards.Select(c => c.Name))}");
    }
}
