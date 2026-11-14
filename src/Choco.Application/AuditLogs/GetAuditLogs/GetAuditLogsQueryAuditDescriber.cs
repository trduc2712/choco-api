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
    public async Task<AuditDescription> DescribeAsync(
        GetAuditLogsQuery request,
        Result<OffsetPageResponse<AuditLogResponse>> response,
        CancellationToken cancellationToken)
    {
        Result<Guid> readerId = await currentUser.GetIdAsync(cancellationToken);

        return new AuditDescription(AuditAction.AuditLogRead, AuditTargetType.AuditLog, readerId.Value);
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        GetAuditLogsQuery request,
        Result<OffsetPageResponse<AuditLogResponse>> response,
        CancellationToken cancellationToken)
    {
        Result<Guid> readerId = await currentUser.GetIdAsync(cancellationToken);
        DateTimeOffset since = timeProvider.GetUtcNow() - TimeSpan.FromMinutes(GetAuditLogsQuery.RecordedReadWindowMinutes);
        AuditLogFilter recentReads = new(
            readerId.Value, AuditAction.AuditLogRead, null, null, null, since, null);

        int recorded = await auditLogRepository.CountForReadAsync(recentReads, cancellationToken);

        return recorded > 0 ? [] : [await DescribeAsync(request, response, cancellationToken)];
    }
}
