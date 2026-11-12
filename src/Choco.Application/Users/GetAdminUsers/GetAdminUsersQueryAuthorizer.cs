using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.GetAdminUsers;

internal sealed class GetAdminUsersQueryAuthorizer : IAuthorizer<GetAdminUsersQuery>
{
    public PermissionRequirement GetRequirement(GetAdminUsersQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyUser, PermissionScope.Platform);
    }
}
