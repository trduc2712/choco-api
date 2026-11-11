namespace Choco.Domain.AuditLogs;

public interface IAuditLogRepository
{
    void Add(AuditLogEntry entry);
}
