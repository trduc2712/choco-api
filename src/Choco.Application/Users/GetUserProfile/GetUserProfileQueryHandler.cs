using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserProfile;

internal sealed class GetUserProfileQueryHandler(ICurrentUser currentUser, IUserRepository userRepository)
    : IQueryHandler<GetUserProfileQuery, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<UserProfileResponse>.Failure(userIdResult.Error);
        }

        UserProfileDetails? profile = await userRepository.GetProfileByIdForReadAsync(
            query.Id, userIdResult.Value, cancellationToken);

        return profile is null
            ? Result<UserProfileResponse>.Failure(UserErrors.NotFound)
            : Result<UserProfileResponse>.Success(profile.ToProfileResponse());
    }
}
