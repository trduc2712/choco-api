using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.UpdateRole;

internal sealed class UpdateRoleCommandAuditDescriber : IAuditDescriber<UpdateRoleCommand, Result<RoleResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        UpdateRoleCommand request, Result<RoleResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.RoleUpdated,
            AuditTargetType.Role,
            response.Value.Id,
            Detail: $"Role: {response.Value.Name}"));
    }
}
