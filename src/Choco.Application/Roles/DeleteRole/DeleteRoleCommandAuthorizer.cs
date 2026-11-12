using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandAuthorizer : IAuthorizer<DeleteRoleCommand>
{
    public PermissionRequirement GetRequirement(DeleteRoleCommand request)
    {
        return new PermissionRequirement(Permission.DeleteRoles, PermissionScope.Platform);
    }
}
