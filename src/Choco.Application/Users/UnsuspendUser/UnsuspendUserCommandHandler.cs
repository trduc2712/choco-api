using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnsuspendUser;

internal sealed class UnsuspendUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    AuditChanges auditChanges) : ICommandHandler<UnsuspendUserCommand>
{
    public async Task<Result> Handle(UnsuspendUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdWithRolesAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        bool wasSuspended = user.IsSuspended;
        user.LiftSuspension();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return auditChanges.Record(Result.Success(),
            wasSuspended == user.IsSuspended ? [] : [AuditChanges.Suspension(false)]);
    }
}
