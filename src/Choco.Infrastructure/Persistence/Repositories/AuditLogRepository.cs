using Choco.Domain.AuditLogs;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ChocoDbContext _dbContext;

    public AuditLogRepository(ChocoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(AuditLogEntry entry)
    {
        _dbContext.Set<AuditLogEntry>().Add(entry);
    }
}
