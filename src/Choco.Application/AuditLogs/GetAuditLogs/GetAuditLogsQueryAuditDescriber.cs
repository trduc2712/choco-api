using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryAuditDescriber(ICurrentUser currentUser)
    : IAuditDescriber<GetAuditLogsQuery, Result<OffsetPageResponse<AuditLogResponse>>>
{
    public async Task<AuditDescription> DescribeAsync(
        GetAuditLogsQuery request,
        Result<OffsetPageResponse<AuditLogResponse>> response,
        CancellationToken cancellationToken)
    {
        Result<Guid> readerId = await currentUser.GetIdAsync(cancellationToken);

        return new AuditDescription(AuditAction.AuditLogRead, AuditTargetType.AuditLog, readerId.Value);
    }
}
