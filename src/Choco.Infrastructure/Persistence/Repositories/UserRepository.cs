using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ChocoDbContext dbContext) : base(dbContext) { }

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
