using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    MyProfileWriter writer,
    IUserRepository users,
    IIdentityProvider identityProvider) : ICommandHandler<UpdateMyProfileCommand, UserProfileResponse>
{
    public Task<Result<UserProfileResponse>> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        return writer.UpdateAsync(async user =>
        {
            User? owner = await users.GetByUsernameForReadAsync(command.Username, cancellationToken);
            if (owner is not null && owner.Id != user.Id)
            {
                return Result.Failure(UserErrors.UsernameTaken);
            }

            Result<IdpUser> result = await identityProvider.UpdateProfileAsync(
                user.IdentityId, command.Username, command.FirstName.Trim(), command.LastName?.Trim() ?? string.Empty, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            user.ChangeProfileName(result.Value.Username!, result.Value.FullName);
            return Result.Success();
        }, cancellationToken);
    }
}
