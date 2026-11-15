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

    public Task<bool> NameExistsAsync(
        string name, Guid? exceptRoleId = null, CancellationToken cancellationToken = default)
    {
        string lowered = name.ToLower();

        return DbSet.AnyAsync(
            role => role.Name.ToLower() == lowered && role.Id != exceptRoleId, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> ListForReadAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleHolderSummary>> ListHolderSummariesForReadAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>().AsNoTracking();

        return await DbSet.AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => new RoleHolderSummary(
                role.Id,
                users.Count(user => role.IsDefault || user.Roles.Any(assignment => assignment.RoleId == role.Id)),
                (from user in users
                 from assignment in user.Roles
                 where !role.IsDefault && assignment.RoleId == role.Id
                 orderby assignment.AssignedAt descending, user.Id
                 select new RoleHolder(user.Id, user.Username, user.DisplayName, user.AvatarUrl))
                .Take(3)
                .ToList()))
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> GetBySystemKeyAsync(string systemKey, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(role => role.SystemKey == systemKey, cancellationToken);
    }
}
