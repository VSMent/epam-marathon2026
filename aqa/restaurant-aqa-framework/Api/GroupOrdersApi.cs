using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>
/// Group pre-order calls: the owner's session and invite, and the guests' sessions and carts. Used by tasks to set
/// up a group, never by steps. Guest calls carry the guest's session token, not a Cognito token.
/// </summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetOwnerGuestSessionIdAsync(string reservationId, string bearerToken) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string bearerToken) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> GetGroupInviteAsync(string inviteToken) =>
        GetAsync(Url($"/guest/invites/{inviteToken}"));

    public Task<IAPIResponse> CreateGuestSessionAsync(string groupReservationId, string guestName) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(new { groupReservationId, guestName }),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string guestSessionId, string dishId, int quantity, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(new { dishId, quantity }),
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> SubmitGuestCartAsync(string guestSessionId, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/submit"), new APIRequestContextOptions
        {
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> GetGroupSessionsAsync(string groupReservationId, string bearerToken) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/sessions"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });
}
