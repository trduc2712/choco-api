using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserFollowing;

internal sealed class GetUserFollowingQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserFollowRepository followRepository)
    : IQueryHandler<GetUserFollowingQuery, PageResponse<UserSearchItemResponse>>
{
    public async Task<Result<PageResponse<UserSearchItemResponse>>> Handle(
        GetUserFollowingQuery query, CancellationToken cancellationToken)
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

        IReadOnlyList<UserFollowEntry> entries = await followRepository.ListFollowingForReadAsync(
            user.Id, caller.Value, UserFollowPages.ReadPosition(query.Cursor), query.Limit + 1, cancellationToken);

        IReadOnlySet<Guid> followed = await FollowStatus.ReadFollowedAsync(
            followRepository, caller.Value, UserFollowPages.PageUserIds(entries, query.Limit), cancellationToken);

        return Result<PageResponse<UserSearchItemResponse>>.Success(
            UserFollowPages.ToPage(entries, query.Limit, followed));
    }
}
