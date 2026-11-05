using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The owner's group pre-order screen (`/reservations/{id}/group-order`). Locators and actions only; the
/// questions read it, and assertions live in the steps.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true });
    public ILocator ParticipantCards => Page.Locator("app-participant-card");
    public ILocator AlcoholFilterButton => Page.Locator(".filter-toggle-btn");
    public ILocator PlanLogisticsButton => Page.Locator(".plan-logistics-btn");
    public ILocator BulkOptimizationCard => Page.Locator(".bulk-optimization-card");
    public ILocator LogisticsDialog => Page.Locator(".modal-card").Filter(new() { HasText = "Plan Transportation Logistics" });

    public Task NavigateAsync(string reservationId) => NavigateToAsync($"/reservations/{reservationId}/group-order");

    public Task WaitUntilLoadedAsync() => Heading.WaitForAsync();

    /// <summary>The participant card of the guest shown under <paramref name="guestName"/>.</summary>
    public ParticipantCardComponent CardOf(string guestName) =>
        new(ParticipantCards.Filter(new() { HasText = guestName }));

    public Task ClickReminderFor(string guestName) => CardOf(guestName).ReminderButton.ClickAsync();
}
