using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.AssignRole;

internal sealed class AssignRoleCommandAuditDescriber(IRoleRepository roleRepository)
    : IAuditDescriber<AssignRoleCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        AssignRoleCommand request, Result response, CancellationToken cancellationToken)
    {
        Role? role = await roleRepository.GetByIdAsync(request.RoleId, cancellationToken);

        return new AuditDescription(
            AuditAction.RoleAssigned,
            AuditTargetType.User,
            request.UserId,
            request.UserId,
            $"Role: {role?.Name ?? request.RoleId.ToString()}");
    }
}
