using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.GetRoleById;

internal sealed class GetRoleByIdQueryAuthorizer : IAuthorizer<GetRoleByIdQuery>
{
    public PermissionRequirement GetRequirement(GetRoleByIdQuery request)
    {
        return new PermissionRequirement(Permission.ReadRoles, PermissionScope.Platform);
    }
}
