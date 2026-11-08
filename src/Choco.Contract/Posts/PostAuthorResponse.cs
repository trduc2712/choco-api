namespace Choco.Contract.Posts;

/// <summary>
/// The user who wrote a post, as shown next to it.
/// </summary>
/// <param name="Username">The author's username, up to 64 characters.</param>
/// <param name="DisplayName">The author's display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The author's avatar URL, or null when no avatar is available.</param>
/// <example>{ "username": "username", "displayName": "Display Name", "avatarUrl": "https://example.com/avatar.png" }</example>
public sealed record PostAuthorResponse(string Username, string DisplayName, string? AvatarUrl);
