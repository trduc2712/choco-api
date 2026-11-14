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

    public Task<int> CountForReadAsync(ReportListFilter filter, CancellationToken cancellationToken)
    {
        return Filter(filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> ListForReadAsync(
        ReportListFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        return await Filter(filter)
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Report> Filter(ReportListFilter filter)
    {
        IQueryable<Report> reports = DbSet.AsNoTracking();

        if (filter.TargetType is { } targetType)
        {
            reports = reports.Where(report => report.TargetType == targetType);
        }

        if (filter.TargetId is { } targetId)
        {
            reports = reports.Where(report => report.TargetPostId == targetId || report.TargetUserId == targetId);
        }

        if (filter.Status is { } status)
        {
            reports = reports.Where(report => report.Status == status);
        }

        if (filter.Reason is { } reason)
        {
            reports = reports.Where(report => report.Reason == reason);
        }

        if (filter.Outcome is { } outcome)
        {
            reports = reports.Where(report => report.Outcome == outcome);
        }

        if (filter.From is { } from)
        {
            reports = reports.Where(report => report.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            reports = reports.Where(report => report.CreatedAt < to);
        }

        if (filter.Search is { } search)
        {
            reports = WhereMatches(reports, search, includeReporter: true);
        }

        return reports;
    }

    private IQueryable<Report> WhereMatches(IQueryable<Report> reports, ReportSearch search, bool includeReporter)
    {
        string namePattern = LikePatterns.Contains(search.Phrase);

        IQueryable<Guid> userIdsByName = DbContext.Set<User>()
            .Where(user =>
                EF.Functions.ILike(EF.Functions.Unaccent(user.Username), EF.Functions.Unaccent(namePattern), LikePatterns.Escape)
                || EF.Functions.ILike(EF.Functions.Unaccent(user.DisplayName), EF.Functions.Unaccent(namePattern), LikePatterns.Escape))
            .Select(user => user.Id);

        IQueryable<Guid> reporterIds = includeReporter
            ? DbContext.Set<User>()
                .Where(user => EF.Functions.ILike(EF.Functions.Unaccent(user.Username), EF.Functions.Unaccent(namePattern), LikePatterns.Escape))
                .Select(user => user.Id)
            : DbContext.Set<User>().Where(user => false).Select(user => user.Id);

        IQueryable<Guid> postIdsByAuthor = DbContext.Set<Post>()
            .Where(post => userIdsByName.Contains(post.AuthorId))
            .Select(post => post.Id);

        IQueryable<Post> postsWithWords = DbContext.Set<Post>();
        foreach (string word in search.Words)
        {
            string wordPattern = LikePatterns.Contains(word);
            postsWithWords = postsWithWords.Where(post => EF.Functions.ILike(
                EF.Functions.Unaccent(post.Content), EF.Functions.Unaccent(wordPattern), LikePatterns.Escape));
        }

        IQueryable<Guid> postIdsByContent = postsWithWords.Select(post => post.Id);

        return reports.Where(report =>
            (report.TargetUserId != null && userIdsByName.Contains(report.TargetUserId.Value))
            || (report.TargetPostId != null
                && (postIdsByAuthor.Contains(report.TargetPostId.Value) || postIdsByContent.Contains(report.TargetPostId.Value)))
            || reporterIds.Contains(report.ReporterId));
    }

    public Task<int> CountOpenAboutUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return DbSet
            .AsNoTracking()
            .CountAsync(report => report.Status == ReportStatus.Open && report.TargetUserId == userId, cancellationToken);
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

        if (filter.Search is { } search)
        {
            open = WhereMatches(open, search, includeReporter: false);
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
