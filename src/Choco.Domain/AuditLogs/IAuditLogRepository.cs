namespace Choco.Domain.AuditLogs;

public interface IAuditLogRepository
{
    void Add(AuditLogEntry entry);

    Task<IReadOnlyList<AuditLogEntry>> ListForReadAsync(
        AuditLogFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForReadAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}
