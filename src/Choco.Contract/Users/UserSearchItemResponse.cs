namespace Choco.Contract.Users;

/// <summary>A user matched by the search.</summary>
/// <param name="Username">The username to use when opening the profile, up to 64 characters.</param>
/// <param name="DisplayName">The original display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <example>{"username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"}</example>
public sealed record UserSearchItemResponse(string Username, string DisplayName, string? AvatarUrl);
