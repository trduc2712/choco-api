using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryAuditDescriber(
    ICurrentUser currentUser,
    IAuditLogRepository auditLogRepository,
    TimeProvider timeProvider)
    : IAuditDescriber<GetAuditLogsQuery, Result<OffsetPageResponse<AuditLogResponse>>>
{
    public Task<AuditDescription> DescribeAsync(
        GetAuditLogsQuery request,
        Result<OffsetPageResponse<AuditLogResponse>> response,
        CancellationToken cancellationToken)
    {
        return AuditLogReads.DescribeAsync(currentUser, cancellationToken);
    }

    public Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        GetAuditLogsQuery request,
        Result<OffsetPageResponse<AuditLogResponse>> response,
        CancellationToken cancellationToken)
    {
        return AuditLogReads.DescribeUnlessRecentAsync(currentUser, auditLogRepository, timeProvider, cancellationToken);
    }
}
