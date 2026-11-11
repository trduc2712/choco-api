using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.FeedbackEntries;

public interface IFeedbackEntryRepository : IRepository<FeedbackEntry>
{
    Task<FeedbackEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedbackEntry>> ListForReadAsync(
        FeedbackStatus? status,
        FeedbackCategory? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountForReadAsync(
        FeedbackStatus? status,
        FeedbackCategory? category,
        CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(FeedbackStatus status, CancellationToken cancellationToken = default);
}
