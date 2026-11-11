using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Reports;

public interface IReportRepository : IRepository<Report>
{
    Task<bool> AddIfNoOpenAsync(Report report, CancellationToken cancellationToken);

    Task<IReadOnlyList<Report>> ListOpenForReadAsync(
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken);
}
