using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListByIdentityIdsWithRolesAsync(
        IReadOnlyCollection<string> identityIds, CancellationToken cancellationToken = default);
}
