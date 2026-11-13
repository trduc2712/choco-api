using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandAuditDescriber : IAuditDescriber<DeleteRoleCommand, Result<RoleResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        DeleteRoleCommand request, Result<RoleResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.RoleDeleted,
            AuditTargetType.Role,
            response.Value.Id,
            Detail: $"Role: {response.Value.Name}",
            Changes: RoleAuditChanges.Between(response.Value, null)));
    }
}
