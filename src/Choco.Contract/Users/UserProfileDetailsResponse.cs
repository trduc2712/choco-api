namespace Choco.Contract.Users;

/// <summary>
/// A user's profile as another signed-in user sees it, with follow status and totals.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The username, up to 64 characters.</param>
/// <param name="DisplayName">The display name, up to 256 characters.</param>
/// <param name="AvatarUrl">The avatar URL, or null when no avatar is available.</param>
/// <param name="IsFollowedByMe">Whether the caller follows this user.</param>
/// <param name="FollowerCount">The number of users who follow this user, zero when nobody does.</param>
/// <param name="FollowingCount">The number of users this user follows, zero when they follow nobody.</param>
/// <param name="PostCount">The number of posts the user has published, zero when they have none. Deleted posts are not counted.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png","isFollowedByMe":true,"followerCount":12,"followingCount":3,"postCount":5}</example>
public sealed record UserProfileDetailsResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsFollowedByMe,
    int FollowerCount,
    int FollowingCount,
    int PostCount);
