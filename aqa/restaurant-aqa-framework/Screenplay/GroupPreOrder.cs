namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>The owner's sign-in details, remembered so the owner can be signed in through the login form.</summary>
public sealed record OwnerAccount(string Email, string Password, string IdToken);

/// <summary>A dish a group scenario uses: its id, the name it is shown under, its price and whether it is alcoholic.</summary>
public sealed record GroupDish(string Id, string Name, decimal Price, bool IsAlcohol);

/// <summary>A guest's session in the group, with the token that authorises that guest's cart calls.</summary>
public sealed record GuestSession(string SessionId, string SessionToken);

/// <summary>The HTTP status the reminder call for a guest came back with.</summary>
public sealed record ReminderDispatch(string GuestName, int Status);

/// <summary>The group pre-order a scenario works with: the reservation, the group, the owner's session, and the
/// dishes and guests the scenario has set up. Tasks add to it as they go; steps read from it.</summary>
public sealed class GroupPreOrder(string reservationId, string groupReservationId, string ownerSessionId, string locationAddress)
{
    /// <summary>The key the standing alcoholic fixture dish is remembered under in <see cref="Dishes"/>.</summary>
    public const string AlcoholicDishKey = "the alcoholic dish";

    public string ReservationId { get; } = reservationId;
    public string GroupReservationId { get; } = groupReservationId;
    public string OwnerSessionId { get; } = ownerSessionId;
    public string LocationAddress { get; } = locationAddress;

    /// <summary>Dishes by the name the scenario uses for them (e.g. "Caesar Salad").</summary>
    public Dictionary<string, GroupDish> Dishes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Guests by the name the scenario uses for them (e.g. "Olena").</summary>
    public Dictionary<string, GuestSession> Guests { get; } = new(StringComparer.OrdinalIgnoreCase);
}
