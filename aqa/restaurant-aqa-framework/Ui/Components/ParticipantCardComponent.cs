using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>`app-participant-card` — one participant on the group pre-order screen, with their dish rows and actions.</summary>
public sealed class ParticipantCardComponent(ILocator root)
{
    public ILocator Root => root;
    public ILocator Title => root.Locator(".title");
    public ILocator AlcoholChip => root.Locator(".alcohol-chip");
    public ILocator EmptyWarning => root.Locator(".empty-warning");
    public ILocator ReminderButton => root.Locator(".reminder-btn");
    public ILocator DishRows => root.Locator(".item");
    public ILocator Subtotal => root.Locator(".subtotal");
    public ILocator NoAlcoholicDrinksNotice => root.Locator("p.empty", new() { HasText = "No alcoholic drinks" });
}
