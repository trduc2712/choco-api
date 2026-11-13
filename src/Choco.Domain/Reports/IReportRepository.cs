using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Reports;

public interface IReportRepository : IRepository<Report>
{
    Task<bool> AddIfNoOpenAsync(Report report, CancellationToken cancellationToken);

    Task<int> CountQueueAsync(ReportQueueFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportQueueGroup>> ListQueueForReadAsync(
        ReportQueueFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<Report?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken);

    Task<int> ResolveOpenAboutTheSameTargetAsync(
        Report report, ReportOutcome outcome, Guid resolverId, DateTimeOffset now, CancellationToken cancellationToken);
}
