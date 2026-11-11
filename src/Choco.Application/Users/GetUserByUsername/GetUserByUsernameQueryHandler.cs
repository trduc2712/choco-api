using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserByUsername;

internal sealed class GetUserByUsernameQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserFollowRepository followRepository,
    IUserBlockRepository blockRepository,
    IPostRepository postRepository)
    : IQueryHandler<GetUserByUsernameQuery, UserProfileDetailsResponse>
{
    public async Task<Result<UserProfileDetailsResponse>> Handle(
        GetUserByUsernameQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<UserProfileDetailsResponse>.Failure(userIdResult.Error);
        }

        User? user = await userRepository.GetByUsernameForReadAsync(query.Username, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileDetailsResponse>.Failure(UserErrors.NotFound);
        }

        UserFollowSummary summary = await followRepository.GetSummaryForReadAsync(
            user.Id, userIdResult.Value, cancellationToken);

        bool isBlockedByViewer = await blockRepository.ExistsForReadAsync(
            userIdResult.Value, user.Id, cancellationToken);

        int postCount = await postRepository.CountByAuthorForReadAsync(user.Id, cancellationToken);

        return Result<UserProfileDetailsResponse>.Success(
            user.ToProfileDetailsResponse(summary, isBlockedByViewer, postCount));
    }
}
