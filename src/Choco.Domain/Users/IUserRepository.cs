using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Users;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdForProfileUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameForReadAsync(string username, CancellationToken cancellationToken = default);
    Task<UserProfileDetails?> GetProfileByIdForReadAsync(Guid id, Guid viewerId, CancellationToken cancellationToken);
    Task<UserProfileDetails?> GetProfileByUsernameForReadAsync(string username, Guid viewerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSearchMatch>> SearchForReadAsync(
        string text,
        Guid excludedUserId,
        (int Rank, string Username, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdentityIdIncludingDeletedAsync(string identityId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListByIdentityIdsWithRolesAsync(
        IReadOnlyCollection<string> identityIds, CancellationToken cancellationToken = default);
}
