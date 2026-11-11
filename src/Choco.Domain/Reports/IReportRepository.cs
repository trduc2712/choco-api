using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Reports;

public interface IReportRepository : IRepository<Report>
{
    Task<bool> AddIfNoOpenAsync(Report report, CancellationToken cancellationToken);

    Task<IReadOnlyList<Report>> ListOpenForReadAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountOpenAsync(CancellationToken cancellationToken);
}
