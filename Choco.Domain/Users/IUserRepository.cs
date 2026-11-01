using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Users;

public interface IUserRepository : IRepository<User>
{
    Task<bool> ExistsByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);
}
