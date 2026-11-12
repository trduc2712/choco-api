using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.SuspendUser;

internal sealed class SuspendUserCommandAuthorizer : IAuthorizer<SuspendUserCommand>
{
    public PermissionRequirement GetRequirement(SuspendUserCommand request)
    {
        return new PermissionRequirement(Permission.SuspendUsers, PermissionScope.Platform);
    }
}
