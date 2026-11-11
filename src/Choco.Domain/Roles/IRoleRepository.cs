using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Roles;

public interface IRoleRepository : IRepository<Role>
{
    Task<IReadOnlyCollection<string>> GetPlatformPermissionsForReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetPlatformRoleNamesForReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Role?> GetBySystemKeyAsync(string systemKey, CancellationToken cancellationToken = default);
}
