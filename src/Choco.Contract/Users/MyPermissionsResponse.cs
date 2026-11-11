namespace Choco.Contract.Users;

/// <summary>
/// The permissions the caller holds.
/// </summary>
/// <param name="Permissions">The permission codes of the default role and of every role assigned to the caller, without duplicates, sorted alphabetically. Empty when the caller holds none.</param>
/// <example>{"permissions":["feedback.read-any","reports.handle"]}</example>
public sealed record MyPermissionsResponse(IReadOnlyList<string> Permissions);
