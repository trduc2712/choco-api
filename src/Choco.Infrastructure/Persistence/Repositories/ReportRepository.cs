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
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken)
    {
        IQueryable<Report> reports = DbSet.AsNoTracking().Where(report => report.Status == ReportStatus.Open);

        if (after is { } position)
        {
            reports = reports.Where(report => EF.Functions.GreaterThan(
                ValueTuple.Create(report.CreatedAt, report.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await reports
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
