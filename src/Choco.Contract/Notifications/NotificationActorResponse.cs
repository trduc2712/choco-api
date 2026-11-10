namespace Choco.Contract.Notifications;

/// <summary>The user whose action caused a notification.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The username to use when opening the profile, up to 64 characters.</param>
/// <param name="DisplayName">The original display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"}</example>
public sealed record NotificationActorResponse(Guid Id, string Username, string DisplayName, string? AvatarUrl);
