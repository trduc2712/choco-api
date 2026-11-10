namespace Choco.Contract.Users;

/// <summary>A user in a search result, a suggestion, or a list of followers or followed users.</summary>
/// <param name="Username">The username to use when opening the profile, up to 64 characters.</param>
/// <param name="DisplayName">The original display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <param name="IsFollowedByMe">Whether the caller follows this user. Always false for the caller's own row.</param>
/// <example>{"username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png","isFollowedByMe":true}</example>
public sealed record UserSearchItemResponse(string Username, string DisplayName, string? AvatarUrl, bool IsFollowedByMe);
