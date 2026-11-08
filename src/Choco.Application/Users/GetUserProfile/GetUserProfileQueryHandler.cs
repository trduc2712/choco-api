using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserProfile;

internal sealed class GetUserProfileQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserFollowRepository followRepository)
    : IQueryHandler<GetUserProfileQuery, UserProfileDetailsResponse>
{
    public async Task<Result<UserProfileDetailsResponse>> Handle(
        GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<UserProfileDetailsResponse>.Failure(userIdResult.Error);
        }

        User? user = await userRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        if (user is null)
        {
            return Result<UserProfileDetailsResponse>.Failure(UserErrors.NotFound);
        }

        UserFollowSummary summary = await followRepository.GetSummaryForReadAsync(
            user.Id, userIdResult.Value, cancellationToken);

        return Result<UserProfileDetailsResponse>.Success(user.ToProfileDetailsResponse(summary));
    }
}
