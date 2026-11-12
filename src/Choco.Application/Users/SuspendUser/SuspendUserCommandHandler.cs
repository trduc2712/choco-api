using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SuspendUser;

internal sealed class SuspendUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    UserSuspender suspender,
    IUnitOfWork unitOfWork) : ICommandHandler<SuspendUserCommand>
{
    public async Task<Result> Handle(SuspendUserCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> actorIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (actorIdResult.IsFailure)
        {
            return actorIdResult;
        }

        User? user = await userRepository.GetByIdWithRolesAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        Result suspension = await suspender.SuspendAsync(user, actorIdResult.Value, command.Reason, cancellationToken);

        if (suspension.IsFailure)
        {
            return suspension;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
