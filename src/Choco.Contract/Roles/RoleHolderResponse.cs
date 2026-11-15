namespace Choco.Contract.Roles;

/// <summary>A user who holds the role.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's public username.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="AvatarUrl">The user's avatar URL, or null.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"}</example>
public sealed record RoleHolderResponse(Guid Id, string Username, string DisplayName, string? AvatarUrl);
