using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SyncUserDeletion;

internal sealed class SyncUserDeletionCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    AuditChanges auditChanges) : ICommandHandler<SyncUserDeletionCommand>
{
    public async Task<Result> Handle(SyncUserDeletionCommand command, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdentityIdIncludingDeletedAsync(command.IdentityId, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return Result.Success();
        }

        userRepository.Remove(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return auditChanges.Record(Result.Success(), []);
    }
}
