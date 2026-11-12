using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnsuspendUser;

internal sealed class UnsuspendUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UnsuspendUserCommand>
{
    public async Task<Result> Handle(UnsuspendUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdWithRolesAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.LiftSuspension();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
