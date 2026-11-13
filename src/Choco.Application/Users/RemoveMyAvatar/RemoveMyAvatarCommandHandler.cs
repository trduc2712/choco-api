using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RemoveMyAvatar;

internal sealed class RemoveMyAvatarCommandHandler(
    MyProfileWriter writer,
    IIdentityProvider identityProvider,
    AuditChanges auditChanges) : ICommandHandler<RemoveMyAvatarCommand, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(RemoveMyAvatarCommand command, CancellationToken cancellationToken)
    {
        List<AuditChange> changes = [];
        Result<UserProfileResponse> profile = await writer.UpdateAsync(async user =>
        {
            Result result = await identityProvider.RemoveAvatarAsync(user.IdentityId, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }

            string? before = user.AvatarUrl;
            user.ChangeAvatar(null);
            AuditChanges.Field(changes, "avatar", before, user.AvatarUrl);
            return Result.Success();
        }, cancellationToken);

        return profile.IsSuccess ? auditChanges.Record(profile, changes) : profile;
    }
}
