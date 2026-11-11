using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserMappings
{
    public static UserProfileResponse ToProfileResponse(this User user)
    {
        return new UserProfileResponse(user.Id, user.Username, user.DisplayName, user.AvatarUrl);
    }

    public static UserProfileDetailsResponse ToProfileDetailsResponse(
        this User user, UserFollowSummary summary, bool isBlockedByViewer, int postCount)
    {
        return new UserProfileDetailsResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.AvatarUrl,
            summary.IsFollowedByViewer,
            isBlockedByViewer,
            summary.FollowerCount,
            summary.FollowingCount,
            postCount);
    }
}
