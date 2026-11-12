namespace Choco.Contract.Users;

/// <summary>
/// The suspension of the signed-in user.
/// </summary>
/// <param name="Reason">Why an administrator suspended the user.</param>
/// <param name="SuspendedAt">When the suspension started, in UTC.</param>
/// <example>{"reason":"Repeated spam","suspendedAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record UserSuspensionResponse(string Reason, DateTimeOffset SuspendedAt);
