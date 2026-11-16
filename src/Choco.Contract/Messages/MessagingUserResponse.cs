namespace Choco.Contract.Messages;

/// <summary>The other user in a direct conversation.</summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="Username">The user's unique username.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="AvatarUrl">The absolute avatar URL, or null if the user has no avatar.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":null}</example>
public sealed record MessagingUserResponse(Guid Id, string Username, string DisplayName, string? AvatarUrl);
