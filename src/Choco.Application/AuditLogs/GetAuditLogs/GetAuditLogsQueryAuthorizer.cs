using Choco.Application.Abstractions.Authorization;
using Choco.Contract.AuditLogs;
using Choco.Domain.Roles;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryAuthorizer : IAuthorizer<GetAuditLogsQuery>
{
    public PermissionRequirement GetRequirement(GetAuditLogsQuery request)
    {
        return new PermissionRequirement(Permission.ReadAuditLog, PermissionScope.Platform);
    }
}
