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
    : IQueryHandler<GetUserFollowersQuery, PageResponse<UserFollowItemResponse>>
{
    public Task<Result<PageResponse<UserFollowItemResponse>>> Handle(GetUserFollowersQuery query, CancellationToken cancellationToken)
    {
        return UserFollowPages.ReadAsync(
            currentUser, userRepository, followRepository.ListFollowersForReadAsync,
            query.UserId, query.Cursor, query.Limit, cancellationToken);
    }
}
