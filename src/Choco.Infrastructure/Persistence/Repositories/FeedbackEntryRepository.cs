using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;

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
        FeedbackFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await Filter(filter)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountForReadAsync(FeedbackFilter filter, CancellationToken cancellationToken = default)
    {
        return Filter(filter).CountAsync(cancellationToken);
    }

    private IQueryable<FeedbackEntry> Filter(FeedbackFilter filter)
    {
        IQueryable<FeedbackEntry> entries = DbSet.AsNoTracking();

        if (filter.Status is { } status)
        {
            entries = entries.Where(entry => entry.Status == status);
        }

        if (filter.Category is { } category)
        {
            entries = entries.Where(entry => entry.Category == category);
        }

        foreach (string term in filter.MessageTerms)
        {
            string pattern = ContainsPattern(term);
            entries = entries.Where(entry => EF.Functions.ILike(
                EF.Functions.Unaccent(entry.Message), EF.Functions.Unaccent(pattern), LikeEscape));
        }

        if (filter.Author is { } author)
        {
            string pattern = ContainsPattern(author);
            IQueryable<Guid> authorIds = DbContext.Set<User>()
                .Where(user =>
                    EF.Functions.ILike(EF.Functions.Unaccent(user.Username), EF.Functions.Unaccent(pattern), LikeEscape)
                    || EF.Functions.ILike(EF.Functions.Unaccent(user.DisplayName), EF.Functions.Unaccent(pattern), LikeEscape))
                .Select(user => user.Id);
            entries = entries.Where(entry => authorIds.Contains(entry.AuthorId));
        }

        return entries;
    }

    private const string LikeEscape = "\\";

    private static string ContainsPattern(string text)
    {
        string escaped = text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    public Task<int> CountByStatusAsync(FeedbackStatus status, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().CountAsync(entry => entry.Status == status, cancellationToken);
    }
}
