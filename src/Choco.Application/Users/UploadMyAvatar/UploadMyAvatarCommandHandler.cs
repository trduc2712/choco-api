using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UploadMyAvatar;

internal sealed class UploadMyAvatarCommandHandler(
    MyProfileWriter writer,
    IIdentityProvider identityProvider,
    AuditChanges auditChanges) : ICommandHandler<UploadMyAvatarCommand, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(UploadMyAvatarCommand command, CancellationToken cancellationToken)
    {
        List<AuditChange> changes = [];
        Result<UserProfileResponse> profile = await writer.UpdateAsync(async user =>
        {
            Result<IdpUser> result = await identityProvider.UploadAvatarAsync(user.IdentityId, command.Image, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            string? before = user.AvatarUrl;
            user.ChangeAvatar(result.Value.AvatarUrl);
            AuditChanges.Field(changes, "avatar", before, user.AvatarUrl);
            return Result.Success();
        }, cancellationToken);

        return profile.IsSuccess ? auditChanges.Record(profile, changes) : profile;
    }
}
