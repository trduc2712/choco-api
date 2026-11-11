using Choco.Domain.Reports;

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

    public async Task<IReadOnlyList<Report>> ListOpenForReadAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(report => report.Status == ReportStatus.Open)
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountOpenAsync(CancellationToken cancellationToken)
    {
        return DbSet.AsNoTracking().CountAsync(report => report.Status == ReportStatus.Open, cancellationToken);
    }
}
