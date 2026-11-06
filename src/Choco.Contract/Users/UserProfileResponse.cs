namespace Choco.Contract.Users;

/// <summary>
/// A user's basic profile, visible to signed-in users.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The username, up to 64 characters.</param>
/// <param name="DisplayName">The display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"}</example>
public sealed record UserProfileResponse(Guid Id, string Username, string DisplayName, string? AvatarUrl);
