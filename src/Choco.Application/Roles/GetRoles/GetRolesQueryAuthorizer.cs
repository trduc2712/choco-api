using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.GetRoles;

internal sealed class GetRolesQueryAuthorizer : IAuthorizer<GetRolesQuery>
{
    public PermissionRequirement GetRequirement(GetRolesQuery request)
    {
        return new PermissionRequirement(Permission.ReadRoles, PermissionScope.Platform);
    }
}
