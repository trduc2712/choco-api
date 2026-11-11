using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.FeedbackEntries;

public interface IFeedbackEntryRepository : IRepository<FeedbackEntry>
{
    Task<FeedbackEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackEntry>> ListForReadAsync(
        FeedbackStatus? status,
        FeedbackCategory? category,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(FeedbackStatus status, CancellationToken cancellationToken = default);
}
