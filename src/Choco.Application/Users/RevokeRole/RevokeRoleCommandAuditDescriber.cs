using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RevokeRole;

internal sealed class RevokeRoleCommandAuditDescriber(IRoleRepository roleRepository, IUserRepository userRepository)
    : IAuditDescriber<RevokeRoleCommand, Result>
{
    private bool _heldBefore;

    public async Task CaptureAsync(RevokeRoleCommand request, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdWithRolesIncludingDeletedForReadAsync(request.UserId, cancellationToken);
        _heldBefore = user?.HoldsRole(request.RoleId) == true;
    }

    public async Task<AuditDescription> DescribeAsync(
        RevokeRoleCommand request, Result response, CancellationToken cancellationToken)
    {
        Role? role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        string roleName = role?.Name ?? request.RoleId.ToString();

        return new AuditDescription(
            AuditAction.RoleRevoked,
            AuditTargetType.User,
            request.UserId,
            request.UserId,
            $"Role: {roleName}",
            _heldBefore ? [new AuditChange("roles", Removed: [roleName])] : []);
    }
}
