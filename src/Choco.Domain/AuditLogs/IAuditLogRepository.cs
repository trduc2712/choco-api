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

    Task<AuditLogEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> ListAroundForReadAsync(
        AuditLogEntry entry,
        int olderCount,
        int newerCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastChangesBeforeForReadAsync(
        AuditLogEntry entry,
        IReadOnlyCollection<string> fields,
        CancellationToken cancellationToken = default);
}
