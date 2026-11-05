namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /reservations/{id}/invite-token — the token guests open the invite link with.</summary>
public sealed record InviteTokenResponse(string Token);

/// <summary>GET /guest/invites/{token} — what a guest (or the owner) sees about the group. Keys come back PascalCase.</summary>
public sealed record GroupInviteResponse(
    string GroupReservationId,
    string LocationId,
    string RestaurantName,
    string LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int MaxGuestCount,
    int TableNumber,
    string InviterName);

/// <summary>GET /reservations/{id}/guest-session-id — the owner's own session in the group.</summary>
public sealed record OwnerGuestSessionResponse(string GuestSessionId);

/// <summary>POST /guest/sessions — a new guest's session, plus the token that authorises its cart calls.</summary>
public sealed record GuestSessionCreatedResponse(string SessionId, string SessionToken);

/// <summary>GET /guest/groups/{id}/sessions — every participant with their items and totals.</summary>
public sealed record GroupSessionsResponse(List<GroupParticipantResponse> Participants, decimal Total);

public sealed record GroupParticipantResponse(
    string Id,
    string Type,
    string Name,
    List<GroupParticipantItemResponse> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record GroupParticipantItemResponse(GroupParticipantDishResponse Dish, int Quantity);

public sealed record GroupParticipantDishResponse(string Id, string Name, decimal Price, bool IsAlcohol);
