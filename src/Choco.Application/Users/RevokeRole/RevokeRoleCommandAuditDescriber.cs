using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RevokeRole;

internal sealed class RevokeRoleCommandAuditDescriber(IRoleRepository roleRepository)
    : IAuditDescriber<RevokeRoleCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        RevokeRoleCommand request, Result response, CancellationToken cancellationToken)
    {
        Role? role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken);

        return new AuditDescription(
            AuditAction.RoleRevoked,
            AuditTargetType.User,
            request.UserId,
            request.UserId,
            $"Role: {role?.Name ?? request.RoleId.ToString()}");
    }
}
