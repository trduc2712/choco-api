using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class ReportRepository : Repository<Report>, IReportRepository
{
    public ReportRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task<bool> AddIfNoOpenAsync(Report report, CancellationToken cancellationToken)
    {
        string targetType = report.TargetType.ToString();
        string reason = report.Reason.ToString();
        string status = report.Status.ToString();

        int inserted = await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO reports (id, reporter_id, target_type, target_post_id, target_user_id, reason, details, created_at, status)
            VALUES ({report.Id}, {report.ReporterId}, {targetType}, {report.TargetPostId}, {report.TargetUserId}, {reason}, {report.Details}, {report.CreatedAt}, {status})
            ON CONFLICT DO NOTHING
            """, cancellationToken);

        if (inserted == 0)
        {
            return false;
        }

        DbSet.Attach(report);
        return true;
    }

    public Task<Report?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public Task<int> ResolveOpenAboutTheSameTargetAsync(
        Report report, ReportOutcome outcome, Guid resolverId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IQueryable<Report> open = DbSet.Where(other => other.Status == ReportStatus.Open);

        open = report.TargetType == ReportTargetType.Post
            ? open.Where(other => other.TargetPostId == report.TargetPostId)
            : open.Where(other => other.TargetUserId == report.TargetUserId);

        return open.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(other => other.Status, ReportStatus.Resolved)
                .SetProperty(other => other.Outcome, (ReportOutcome?)outcome)
                .SetProperty(other => other.ResolvedById, (Guid?)resolverId)
                .SetProperty(other => other.ResolvedAt, (DateTimeOffset?)now),
            cancellationToken);
    }

    public Task<int> CountQueueAsync(ReportQueueFilter filter, CancellationToken cancellationToken)
    {
        return Groups(filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReportQueueGroup>> ListQueueForReadAsync(
        ReportQueueFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        List<TargetKey> keys = await Groups(filter)
            .OrderBy(group => group.Min(report => report.CreatedAt))
            .ThenBy(group => group.Key.PostId ?? group.Key.UserId)
            .Skip(skip)
            .Take(take)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        if (keys.Count == 0)
        {
            return [];
        }

        List<Guid> postIds = keys.Where(key => key.PostId != null).Select(key => key.PostId!.Value).ToList();
        List<Guid> userIds = keys.Where(key => key.UserId != null).Select(key => key.UserId!.Value).ToList();

        List<Report> reports = await OpenReportsOfExistingTargets()
            .Where(report => (report.TargetPostId != null && postIds.Contains(report.TargetPostId.Value))
                || (report.TargetUserId != null && userIds.Contains(report.TargetUserId.Value)))
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

        return keys
            .Select(key =>
            {
                Guid targetId = (key.PostId ?? key.UserId)!.Value;

                return new ReportQueueGroup(
                    key.Type,
                    targetId,
                    reports.Where(report => (report.TargetPostId ?? report.TargetUserId) == targetId).ToList());
            })
            .ToList();
    }

    private IQueryable<Report> OpenReportsOfExistingTargets()
    {
        IQueryable<Post> posts = DbContext.Set<Post>();
        IQueryable<User> users = DbContext.Set<User>();

        return DbSet
            .AsNoTracking()
            .Where(report => report.Status == ReportStatus.Open)
            .Where(report => (report.TargetPostId != null && posts.Any(post => post.Id == report.TargetPostId))
                || (report.TargetUserId != null && users.Any(user => user.Id == report.TargetUserId)));
    }

    private IQueryable<IGrouping<TargetKey, Report>> Groups(ReportQueueFilter filter)
    {
        IQueryable<Report> open = OpenReportsOfExistingTargets();

        if (filter.TargetType is { } targetType)
        {
            open = open.Where(report => report.TargetType == targetType);
        }

        IQueryable<IGrouping<TargetKey, Report>> groups = open.GroupBy(report => new TargetKey
        {
            Type = report.TargetType,
            PostId = report.TargetPostId,
            UserId = report.TargetUserId
        });

        if (filter.Reason is { } reason)
        {
            groups = groups.Where(group => group.Count(report => report.Reason == reason) > 0);
        }

        return groups;
    }

    private sealed class TargetKey
    {
        public ReportTargetType Type { get; init; }

        public Guid? PostId { get; init; }

        public Guid? UserId { get; init; }
    }
}
