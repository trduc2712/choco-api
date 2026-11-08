using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UploadMyAvatar;

internal sealed class UploadMyAvatarCommandHandler(
    MyProfileWriter writer,
    IIdentityProvider identityProvider) : ICommandHandler<UploadMyAvatarCommand, UserProfileResponse>
{
    public Task<Result<UserProfileResponse>> Handle(UploadMyAvatarCommand command, CancellationToken cancellationToken)
    {
        return writer.UpdateAsync(async user =>
        {
            Result<IdpUser> result = await identityProvider.UploadAvatarAsync(user.IdentityId, command.Image, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            user.ChangeAvatar(result.Value.AvatarUrl);
            return Result.Success();
        }, cancellationToken);
    }
}
