using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.GetAdminUser;

internal sealed class GetAdminUserQueryAuthorizer : IAuthorizer<GetAdminUserQuery>
{
    public PermissionRequirement GetRequirement(GetAdminUserQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyUser, PermissionScope.Platform);
    }
}
