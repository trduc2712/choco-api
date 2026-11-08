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
    : IQueryHandler<GetUserFollowingQuery, PageResponse<UserFollowItemResponse>>
{
    public Task<Result<PageResponse<UserFollowItemResponse>>> Handle(GetUserFollowingQuery query, CancellationToken cancellationToken)
    {
        return UserFollowPages.ReadAsync(
            currentUser, userRepository, followRepository.ListFollowingForReadAsync,
            query.UserId, query.Cursor, query.Limit, cancellationToken);
    }
}
