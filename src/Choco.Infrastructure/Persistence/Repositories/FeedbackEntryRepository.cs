using Choco.Domain.FeedbackEntries;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackEntryRepository : Repository<FeedbackEntry>, IFeedbackEntryRepository
{
    public FeedbackEntryRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public Task<FeedbackEntry?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackEntry>> ListForReadAsync(
        FeedbackStatus? status,
        FeedbackCategory? category,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await Filter(status, category)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountForReadAsync(
        FeedbackStatus? status,
        FeedbackCategory? category,
        CancellationToken cancellationToken = default)
    {
        return Filter(status, category).CountAsync(cancellationToken);
    }

    private IQueryable<FeedbackEntry> Filter(FeedbackStatus? status, FeedbackCategory? category)
    {
        IQueryable<FeedbackEntry> entries = DbSet.AsNoTracking();

        if (status is { } statusFilter)
        {
            entries = entries.Where(entry => entry.Status == statusFilter);
        }

        if (category is { } categoryFilter)
        {
            entries = entries.Where(entry => entry.Category == categoryFilter);
        }

        return entries;
    }

    public Task<int> CountByStatusAsync(FeedbackStatus status, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().CountAsync(entry => entry.Status == status, cancellationToken);
    }
}
