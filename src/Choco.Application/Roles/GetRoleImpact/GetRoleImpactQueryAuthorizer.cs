using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.GetRoleImpact;

internal sealed class GetRoleImpactQueryAuthorizer : IAuthorizer<GetRoleImpactQuery>
{
    public PermissionRequirement GetRequirement(GetRoleImpactQuery request)
    {
        return new PermissionRequirement(Permission.UpdateRoles, PermissionScope.Platform);
    }
}
