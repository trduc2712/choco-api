using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users;

internal static class UserFollowPages
{
    public static async Task<Result<PageResponse<UserFollowItemResponse>>> ReadAsync(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        Func<Guid, Guid?, int, CancellationToken, Task<IReadOnlyList<User>>> readPageAsync,
        Guid userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<UserFollowItemResponse>>.Failure(caller.Error);
        }

        if (await userRepository.GetByIdForReadAsync(userId, cancellationToken) is null)
        {
            return Result<PageResponse<UserFollowItemResponse>>.Failure(UserErrors.NotFound);
        }

        Guid? after = cursor is null ? null : Guid.ParseExact(cursor, "N");
        IReadOnlyList<User> users = await readPageAsync(userId, after, limit + 1, cancellationToken);
        List<UserFollowItemResponse> items = users.Take(limit)
            .Select(user => new UserFollowItemResponse(user.Id, user.Username, user.DisplayName, user.AvatarUrl))
            .ToList();
        string? nextCursor = users.Count > limit ? items[^1].Id.ToString("N") : null;

        return Result<PageResponse<UserFollowItemResponse>>.Success(new PageResponse<UserFollowItemResponse>(items, nextCursor));
    }
}
