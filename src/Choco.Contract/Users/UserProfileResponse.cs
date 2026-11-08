namespace Choco.Contract.Users;

/// <summary>A user's profile and follow details, visible to signed-in users.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The username, up to 64 characters.</param>
/// <param name="DisplayName">The display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <param name="FollowerCount">Number of active accounts following the user.</param>
/// <param name="FollowingCount">Number of active accounts the user follows.</param>
/// <param name="IsFollowedByMe">Whether the signed-in caller follows the user. False on the caller's own profile.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png","followerCount":2,"followingCount":1,"isFollowedByMe":true}</example>
public sealed record UserProfileResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByMe);
