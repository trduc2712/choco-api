using System.Text.Json;

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

    public Task<AuditLogEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAroundForReadAsync(
        AuditLogEntry entry,
        int olderCount,
        int newerCount,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLogEntry> sameTarget = _dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .Where(other => other.TargetType == entry.TargetType && other.TargetId == entry.TargetId && other.Id != entry.Id);

        IQueryable<AuditLogEntry> older = sameTarget
            .Where(other => other.OccurredAt <= entry.OccurredAt)
            .OrderByDescending(other => other.OccurredAt)
            .Take(olderCount);

        IQueryable<AuditLogEntry> newer = sameTarget
            .Where(other => other.OccurredAt > entry.OccurredAt)
            .OrderBy(other => other.OccurredAt)
            .Take(newerCount);

        List<AuditLogEntry> around = await older.Concat(newer).ToListAsync(cancellationToken);

        return around.OrderBy(other => other.OccurredAt).ThenBy(other => other.Id).ToList();
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetLastChangesBeforeForReadAsync(
        AuditLogEntry entry,
        IReadOnlyCollection<string> fields,
        CancellationToken cancellationToken = default)
    {
        if (fields.Count == 0)
        {
            return new Dictionary<string, DateTimeOffset>();
        }

        string targetType = entry.TargetType.ToString();
        string[] fieldNames = [.. fields];

        string json = await _dbContext.Database.SqlQuery<string>($$"""
            SELECT coalesce(jsonb_object_agg(latest.field, latest.changed_at), '{}'::jsonb)::text AS "Value"
            FROM (
                SELECT change->>'field' AS field, max(logged.occurred_at) AS changed_at
                FROM audit_logs AS logged, jsonb_array_elements(logged.changes) AS change
                WHERE logged.target_type = {{targetType}}
                    AND logged.target_id = {{entry.TargetId}}
                    AND logged.occurred_at < {{entry.OccurredAt}}
                    AND change->>'field' = ANY({{fieldNames}})
                GROUP BY change->>'field'
            ) AS latest
            """).SingleAsync(cancellationToken);

        return JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(json)!;
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
