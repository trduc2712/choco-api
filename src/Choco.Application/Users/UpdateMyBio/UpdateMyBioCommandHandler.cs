using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyBio;

internal sealed class UpdateMyBioCommandHandler(MyProfileWriter writer, AuditChanges auditChanges)
    : ICommandHandler<UpdateMyBioCommand, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(UpdateMyBioCommand command, CancellationToken cancellationToken)
    {
        List<AuditChange> changes = [];
        Result<UserProfileResponse> profile = await writer.UpdateAsync(user =>
        {
            string? before = user.Bio;
            user.ChangeBio(command.Bio);
            AuditChanges.Field(changes, "bio", before, user.Bio);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

        return profile.IsSuccess ? auditChanges.Record(profile, changes) : profile;
    }
}
