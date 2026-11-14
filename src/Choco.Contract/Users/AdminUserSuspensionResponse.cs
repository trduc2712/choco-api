namespace Choco.Contract.Users;

/// <summary>
/// The suspension of a user.
/// </summary>
/// <param name="Reason">Why the user was suspended. The suspended user sees it too.</param>
/// <param name="SuspendedAt">When the user was suspended, in UTC.</param>
/// <param name="SuspendedBy">The user who suspended them. Null when that account no longer exists.</param>
/// <example>{"reason":"Reported for spam","suspendedAt":"2026-01-02T00:00:00+00:00","suspendedBy":{"id":"00000000-0000-0000-0000-000000000002","username":"username-2"}}</example>
public sealed record AdminUserSuspensionResponse(
    string Reason,
    DateTimeOffset SuspendedAt,
    AdminUserReferenceResponse? SuspendedBy);
