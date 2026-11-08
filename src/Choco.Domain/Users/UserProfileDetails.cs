namespace Choco.Domain.Users;

public sealed record UserProfileDetails(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByMe);
