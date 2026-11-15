using Choco.Application.Abstractions.Authorization;
using Choco.Contract.AuditLogs;
using Choco.Domain.Roles;

namespace Choco.Application.AuditLogs.GetAuditLog;

internal sealed class GetAuditLogQueryAuthorizer : IAuthorizer<GetAuditLogQuery>
{
    public PermissionRequirement GetRequirement(GetAuditLogQuery request)
    {
        return new PermissionRequirement(Permission.ReadAuditLog, PermissionScope.Platform);
    }
}
