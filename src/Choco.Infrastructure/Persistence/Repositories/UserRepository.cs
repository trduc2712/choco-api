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
}
