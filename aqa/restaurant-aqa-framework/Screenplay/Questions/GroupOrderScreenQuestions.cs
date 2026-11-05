using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>One dish row on a guest's card as shown: the dish name, whether it is marked alcoholic, and its price text.</summary>
public sealed record GuestDishLine(string DishName, bool IsAlcoholic, string PriceText);

/// <summary>One guest's card as shown on the group pre-order screen.</summary>
/// <param name="ReminderLabel">The text of the reminder button, or empty when the card has no reminder button.</param>
/// <param name="Subtotal">The subtotal line's text, e.g. "Subtotal $15.00".</param>
public sealed record GuestCard(
    string Name,
    bool MarkedAlcoholic,
    bool NoDishesWarningShown,
    string ReminderLabel,
    bool NoAlcoholicDrinksNoticeShown,
    string Subtotal,
    IReadOnlyList<GuestDishLine> Lines);

/// <summary>The participant cards shown, in the order they appear. Waits until the screen has loaded.</summary>
public sealed class TheGuestCards : IQuestion<IReadOnlyList<GuestCard>>
{
    public async Task<IReadOnlyList<GuestCard>> AnsweredByAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.WaitUntilLoadedAsync();
        await screen.ParticipantCards.First.WaitForAsync();

        var cards = new List<GuestCard>();
        foreach (var locator in await screen.ParticipantCards.AllAsync())
        {
            var card = new ParticipantCardComponent(locator);

            var lines = new List<GuestDishLine>();
            foreach (var row in await card.DishRows.AllAsync())
            {
                var name = (await row.Locator(".name").InnerTextAsync()).Replace("🍷", "").Trim();
                var isAlcoholic = await row.Locator(".alcohol-tag").CountAsync() > 0;
                var price = (await row.Locator(".price").InnerTextAsync()).Trim();
                lines.Add(new GuestDishLine(name, isAlcoholic, price));
            }

            var reminderLabel = await card.ReminderButton.CountAsync() == 0
                ? ""
                : (await card.ReminderButton.InnerTextAsync()).Trim();

            cards.Add(new GuestCard(
                Name: (await card.Title.InnerTextAsync()).Trim(),
                MarkedAlcoholic: await card.AlcoholChip.CountAsync() > 0,
                NoDishesWarningShown: await card.EmptyWarning.CountAsync() > 0,
                ReminderLabel: reminderLabel,
                NoAlcoholicDrinksNoticeShown: await card.NoAlcoholicDrinksNotice.CountAsync() > 0,
                Subtotal: (await card.Subtotal.InnerTextAsync()).Trim(),
                Lines: lines));
        }

        return cards;
    }
}

/// <summary>The text of the bulk-sharing suggestion card, or empty when no suggestion is offered.</summary>
public sealed class TheBulkSuggestionText : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.WaitUntilLoadedAsync();
        return await screen.BulkOptimizationCard.CountAsync() == 0 ? "" : await screen.BulkOptimizationCard.InnerTextAsync();
    }
}

/// <summary>What the "Plan Transportation Logistics" dialog offers: the venue address and the ride-sharing partners.</summary>
public sealed record LogisticsOptions(string VenueAddress, IReadOnlyList<string> Partners);

public sealed class TheLogisticsOptions : IQuestion<LogisticsOptions>
{
    public async Task<LogisticsOptions> AnsweredByAsync(Actor actor)
    {
        var screen = new GroupOrderPage(actor.LookingAtThePage());
        await screen.LogisticsDialog.WaitForAsync();

        var address = (await screen.LogisticsDialog.Locator(".dest-address strong").InnerTextAsync()).Trim();
        var partners = await screen.LogisticsDialog.Locator(".partner-card strong").AllInnerTextsAsync();
        return new LogisticsOptions(address, partners.Select(p => p.Trim()).ToList());
    }
}

/// <summary>The success toasts that showed up, waiting briefly for one to appear.</summary>
public sealed class TheSuccessToasts : IQuestion<IReadOnlyList<string>>
{
    private static readonly float PatienceMs = 5000;

    public async Task<IReadOnlyList<string>> AnsweredByAsync(Actor actor)
    {
        var toasts = new ToastComponent(actor.LookingAtThePage());
        try
        {
            await toasts.Success.First.WaitForAsync(new() { Timeout = PatienceMs });
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            // No toast came up in time; the answer is simply an empty list.
        }

        return await toasts.Success.AllInnerTextsAsync();
    }
}
