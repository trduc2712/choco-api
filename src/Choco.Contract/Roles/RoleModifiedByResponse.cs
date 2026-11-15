namespace Choco.Contract.Roles;

/// <summary>The user who last created or updated the role.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's username.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000003","username":"username-2"}</example>
public sealed record RoleModifiedByResponse(Guid Id, string Username);
