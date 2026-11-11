namespace Choco.Domain.AuditLogs;

public interface IAuditLogRepository
{
    void Add(AuditLogEntry entry);

    Task<IReadOnlyList<AuditLogEntry>> ListForReadAsync(
        AuditLogFilter filter,
        (DateTimeOffset OccurredAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);
}
