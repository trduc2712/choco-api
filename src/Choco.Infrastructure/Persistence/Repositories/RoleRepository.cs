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
        return await PlatformRoles(userId)
            .SelectMany(role => role.Permissions)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetPlatformRoleNamesForReadAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await PlatformRoles(userId)
            .Select(role => role.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Role> PlatformRoles(Guid userId)
    {
        IQueryable<Guid> assignedRoleIds = DbContext.Set<User>()
            .Where(user => user.Id == userId)
            .SelectMany(user => user.Roles.Select(userRole => userRole.RoleId));

        return DbSet
            .AsNoTracking()
            .Where(role => role.IsDefault || assignedRoleIds.Contains(role.Id));
    }

    public Task<Role?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        string lowered = name.ToLower();

        return DbSet.AnyAsync(role => role.Name.ToLower() == lowered, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> ListForReadAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> GetBySystemKeyAsync(string systemKey, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(role => role.SystemKey == systemKey, cancellationToken);
    }
}
