using Choco.Contract.Users;
using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserMappings
{
    public static UserProfileResponse ToProfileResponse(this UserProfileDetails profile)
    {
        return new UserProfileResponse(
            profile.Id, profile.Username, profile.DisplayName, profile.AvatarUrl,
            profile.FollowerCount, profile.FollowingCount, profile.IsFollowedByMe);
    }
}
