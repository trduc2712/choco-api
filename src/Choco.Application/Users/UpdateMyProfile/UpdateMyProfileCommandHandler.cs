using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    MyProfileWriter writer,
    IUserRepository users,
    IIdentityProvider identityProvider,
    AuditChanges auditChanges) : ICommandHandler<UpdateMyProfileCommand, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        List<AuditChange> changes = [];
        Result<UserProfileResponse> profile = await writer.UpdateAsync(async user =>
        {
            User? owner = await users.GetByUsernameForReadAsync(command.Username, cancellationToken);
            if (owner is not null && owner.Id != user.Id)
            {
                return Result.Failure(UserErrors.UsernameTaken);
            }

            IdpUser? before = await identityProvider.GetUserAsync(user.IdentityId, cancellationToken);
            if (before is null)
            {
                return Result.Failure(UserErrors.Deleted);
            }

            Result<IdpUser> result = await identityProvider.UpdateProfileAsync(
                user.IdentityId, command.Username, command.FirstName.Trim(), command.LastName?.Trim() ?? string.Empty, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            IdpUser after = result.Value;
            AuditChanges.Field(changes, "username", before.Username, after.Username);
            AuditChanges.Field(changes, "firstName", before.FirstName ?? string.Empty, after.FirstName ?? string.Empty);
            AuditChanges.Field(changes, "lastName", before.LastName ?? string.Empty, after.LastName ?? string.Empty);
            user.ChangeProfileName(after.Username!, after.FullName);
            return Result.Success();
        }, cancellationToken);

        return profile.IsSuccess ? auditChanges.Record(profile, changes) : profile;
    }
}
