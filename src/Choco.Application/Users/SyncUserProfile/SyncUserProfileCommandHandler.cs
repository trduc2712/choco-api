using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SyncUserProfile;

internal sealed class SyncUserProfileCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    AuditChanges auditChanges) : ICommandHandler<SyncUserProfileCommand>
{
    public async Task<Result> Handle(SyncUserProfileCommand command, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdentityIdIncludingDeletedAsync(command.IdentityId, cancellationToken);

        if (user is null || user.IsDeleted || user.IsStaleIdentityEvent(command.IdentityUpdatedAt))
        {
            return Result.Success();
        }

        if (user.Username != command.Username)
        {
            User? owner = await userRepository.GetByUsernameForReadAsync(command.Username, cancellationToken);

            if (owner is not null && owner.Id != user.Id)
            {
                return Result.Failure(UserErrors.UsernameTaken);
            }
        }

        string previousUsername = user.Username;
        string previousDisplayName = user.DisplayName;
        string? previousAvatarUrl = user.AvatarUrl;

        user.SyncFromIdentity(command.Username, command.DisplayName, command.AvatarUrl, command.IdentityUpdatedAt);

        List<AuditChange> changes = [];
        AuditChanges.Field(changes, "username", previousUsername, user.Username);
        AuditChanges.Field(changes, "displayName", previousDisplayName, user.DisplayName);
        AuditChanges.Field(changes, "avatar", previousAvatarUrl, user.AvatarUrl);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return changes.Count == 0 ? Result.Success() : auditChanges.Record(Result.Success(), changes);
    }
}
