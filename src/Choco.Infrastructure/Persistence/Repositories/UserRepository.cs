using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task<User?> GetByIdForProfileUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Hold the row lock while writing to the identity provider, then persist in the same order.
        User? user = await DbSet.FromSql($"SELECT * FROM users WHERE id = {id} AND NOT is_deleted FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not null)
        {
            // Resolving the caller may have tracked the row before the lock was acquired.
            await DbContext.Entry(user).ReloadAsync(cancellationToken);
        }

        return user;
    }

    public Task<User?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListByIdsForReadAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(user => ids.Contains(user.Id)).ToListAsync(cancellationToken);
    }

    public Task<User?> GetByUsernameForReadAsync(string username, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSearchMatch>> SearchForReadAsync(
        string text,
        Guid excludedUserId,
        (int Rank, string Username, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        string usernameText = text.ToLowerInvariant();
        var matches =
            from user in DbSet.AsNoTracking()
            where user.Id != excludedUserId
            let nameText = EF.Functions.Unaccent(text).ToLower()
            let username = user.Username.ToLower()
            let name = EF.Functions.Unaccent(user.DisplayName).ToLower()
            let usernamePosition = username.IndexOf(usernameText)
            let namePosition = nameText != string.Empty ? name.IndexOf(nameText) : -1
            where usernamePosition >= 0 || namePosition >= 0
            let rank = username == usernameText ? 0
                : namePosition == 0 && name == nameText ? 1
                : usernamePosition == 0 || namePosition == 0 ? 2
                : 3
            select new { user.Id, user.Username, user.DisplayName, user.AvatarUrl, Rank = rank };

        if (after is { } position)
        {
            matches = matches.Where(match => EF.Functions.GreaterThan(
                ValueTuple.Create(match.Rank, match.Username, match.Id),
                ValueTuple.Create(position.Rank, position.Username, position.Id)));
        }

        return await matches
            .OrderBy(match => match.Rank)
            .ThenBy(match => match.Username)
            .ThenBy(match => match.Id)
            .Take(count)
            .Select(match => new UserSearchMatch(match.Id, match.Username, match.DisplayName, match.AvatarUrl, match.Rank))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .IgnoreQueryFilters()
            .AnyAsync(user => user.IdentityId == identityId, cancellationToken);
    }

    public Task<User?> GetByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.IdentityId == identityId, cancellationToken);
    }

    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<int> CountWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return DbSet.CountAsync(user => user.Roles.Any(userRole => userRole.RoleId == roleId), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListByIdentityIdsWithRolesAsync(
        IReadOnlyCollection<string> identityIds, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(user => user.Roles)
            .Where(user => identityIds.Contains(user.IdentityId))
            .ToListAsync(cancellationToken);
    }
}
