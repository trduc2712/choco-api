namespace Choco.Domain.UserFollows;

public sealed record UserFollowSummary(bool IsFollowedByViewer, int FollowerCount, int FollowingCount);
