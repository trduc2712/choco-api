using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserFollowers;

internal sealed class GetUserFollowersQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserFollowRepository followRepository)
    : IQueryHandler<GetUserFollowersQuery, PageResponse<UserSearchItemResponse>>
{
    public async Task<Result<PageResponse<UserSearchItemResponse>>> Handle(
        GetUserFollowersQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<UserSearchItemResponse>>.Failure(caller.Error);
        }

        User? user = await userRepository.GetByIdForReadAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return Result<PageResponse<UserSearchItemResponse>>.Failure(UserErrors.NotFound);
        }

        IReadOnlyList<UserFollowEntry> entries = await followRepository.ListFollowersForReadAsync(
            user.Id, UserFollowPages.ReadPosition(query.Cursor), query.Limit + 1, cancellationToken);

        IReadOnlySet<Guid> followed = await FollowStatus.ReadFollowedAsync(
            followRepository, caller.Value, UserFollowPages.PageUserIds(entries, query.Limit), cancellationToken);

        return Result<PageResponse<UserSearchItemResponse>>.Success(
            UserFollowPages.ToPage(entries, query.Limit, followed));
    }
}
