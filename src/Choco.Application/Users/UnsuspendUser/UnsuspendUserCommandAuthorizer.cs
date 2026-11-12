using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.UnsuspendUser;

internal sealed class UnsuspendUserCommandAuthorizer : IAuthorizer<UnsuspendUserCommand>
{
    public PermissionRequirement GetRequirement(UnsuspendUserCommand request)
    {
        return new PermissionRequirement(Permission.SuspendUsers, PermissionScope.Platform);
    }
}
