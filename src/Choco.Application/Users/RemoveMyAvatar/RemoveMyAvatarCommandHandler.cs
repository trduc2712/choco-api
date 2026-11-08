using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RemoveMyAvatar;

internal sealed class RemoveMyAvatarCommandHandler(
    MyProfileWriter writer,
    IIdentityProvider identityProvider) : ICommandHandler<RemoveMyAvatarCommand, UserProfileResponse>
{
    public Task<Result<UserProfileResponse>> Handle(RemoveMyAvatarCommand command, CancellationToken cancellationToken)
    {
        return writer.UpdateAsync(async user =>
        {
            Result result = await identityProvider.RemoveAvatarAsync(user.IdentityId, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }

            user.ChangeAvatar(null);
            return Result.Success();
        }, cancellationToken);
    }
}
