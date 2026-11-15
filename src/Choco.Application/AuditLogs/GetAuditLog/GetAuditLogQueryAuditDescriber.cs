using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLog;

internal sealed class GetAuditLogQueryAuditDescriber(
    ICurrentUser currentUser,
    IAuditLogRepository auditLogRepository,
    TimeProvider timeProvider)
    : IAuditDescriber<GetAuditLogQuery, Result<AuditLogResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        GetAuditLogQuery request,
        Result<AuditLogResponse> response,
        CancellationToken cancellationToken)
    {
        return AuditLogReads.DescribeAsync(currentUser, cancellationToken);
    }

    public Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        GetAuditLogQuery request,
        Result<AuditLogResponse> response,
        CancellationToken cancellationToken)
    {
        return AuditLogReads.DescribeUnlessRecentAsync(currentUser, auditLogRepository, timeProvider, cancellationToken);
    }
}
