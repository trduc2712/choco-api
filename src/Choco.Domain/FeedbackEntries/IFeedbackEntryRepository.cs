using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.FeedbackEntries;

public interface IFeedbackEntryRepository : IRepository<FeedbackEntry>
{
    Task<FeedbackEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackEntry>> ListForReadAsync(
        FeedbackFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForReadAsync(FeedbackFilter filter, CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(FeedbackStatus status, CancellationToken cancellationToken = default);
}
