using Choco.Contract.Users;
using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserMappings
{
    public static UserProfileResponse ToProfileResponse(this User user)
    {
        return new UserProfileResponse(user.Id, user.Username, user.DisplayName, user.AvatarUrl);
    }
}
