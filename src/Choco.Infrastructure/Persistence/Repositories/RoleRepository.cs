using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository : Repository<Role>, IRoleRepository
{
    public RoleRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task<IReadOnlyCollection<string>> GetPlatformPermissionsForReadAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        IQueryable<Guid> assignedRoleIds = DbContext.Set<User>()
            .Where(user => user.Id == userId)
            .SelectMany(user => user.Roles.Select(userRole => userRole.RoleId));

        return await DbSet
            .AsNoTracking()
            .Where(role => role.IsDefault || assignedRoleIds.Contains(role.Id))
            .SelectMany(role => role.Permissions)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> GetBySystemKeyAsync(string systemKey, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(role => role.SystemKey == systemKey, cancellationToken);
    }
}
