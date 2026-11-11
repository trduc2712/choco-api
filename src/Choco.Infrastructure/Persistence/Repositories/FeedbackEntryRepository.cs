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
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
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

        if (after is { } position)
        {
            entries = entries.Where(entry => EF.Functions.LessThan(
                ValueTuple.Create(entry.CreatedAt, entry.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
