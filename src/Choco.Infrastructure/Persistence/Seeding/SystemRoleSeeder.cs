using Choco.Domain.Roles;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Seeding;

internal static class SystemRoleSeeder
{
    public static void Seed(DbContext dbContext, bool storeOperationPerformed)
    {
        HashSet<string?> existingKeys = dbContext.Set<Role>()
            .Select(role => role.SystemKey)
            .Where(key => key != null)
            .ToHashSet();

        dbContext.Set<Role>().AddRange(MissingRoles(existingKeys));
        dbContext.SaveChanges();
    }

    public static async Task SeedAsync(DbContext dbContext, bool storeOperationPerformed, CancellationToken cancellationToken)
    {
        HashSet<string?> existingKeys = (await dbContext.Set<Role>()
            .Select(role => role.SystemKey)
            .Where(key => key != null)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        dbContext.Set<Role>().AddRange(MissingRoles(existingKeys));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Role> MissingRoles(HashSet<string?> existingKeys)
    {
        return SystemRoles.All
            .Where(definition => !existingKeys.Contains(definition.Key))
            .Select(Role.CreateSystem);
    }
}
