using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Permissions;
using Choco.Domain.Roles;

namespace Choco.Application.Permissions.GetPermissions;

internal sealed class GetPermissionsQueryAuthorizer : IAuthorizer<GetPermissionsQuery>
{
    public PermissionRequirement GetRequirement(GetPermissionsQuery request)
    {
        return new PermissionRequirement(Permission.ReadRoles, PermissionScope.Platform);
    }
}
