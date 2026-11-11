using Choco.Domain.AuditLogs;

using Microsoft.EntityFrameworkCore;

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

    public async Task<IReadOnlyList<AuditLogEntry>> ListForReadAsync(
        AuditLogFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await Filter(filter)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountForReadAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        return Filter(filter).CountAsync(cancellationToken);
    }

    private IQueryable<AuditLogEntry> Filter(AuditLogFilter filter)
    {
        IQueryable<AuditLogEntry> entries = _dbContext.Set<AuditLogEntry>().AsNoTracking();

        if (filter.ActorId is { } actorId)
        {
            entries = entries.Where(entry => entry.ActorId == actorId);
        }

        if (filter.Action is { } action)
        {
            entries = entries.Where(entry => entry.Action == action);
        }

        if (filter.TargetType is { } targetType)
        {
            entries = entries.Where(entry => entry.TargetType == targetType);
        }

        if (filter.TargetId is { } targetId)
        {
            entries = entries.Where(entry => entry.TargetId == targetId);
        }

        if (filter.AffectedUserId is { } affectedUserId)
        {
            entries = entries.Where(entry => entry.AffectedUserId == affectedUserId);
        }

        if (filter.From is { } from)
        {
            entries = entries.Where(entry => entry.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            entries = entries.Where(entry => entry.OccurredAt < to);
        }

        return entries;
    }
}
