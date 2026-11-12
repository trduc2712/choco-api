using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SuspendUser;

internal sealed class SuspendUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SuspendUserCommand>
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

        IReadOnlyCollection<string> actorPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(actorIdResult.Value, cancellationToken);
        IReadOnlyCollection<string> userPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(user.Id, cancellationToken);

        Result check = user.CheckCanBeSuspendedBy(actorIdResult.Value, actorPermissions, userPermissions);

        if (check.IsFailure)
        {
            return check;
        }

        Role? administratorRole = await roleRepository.GetBySystemKeyAsync(SystemRoles.Admin, cancellationToken);

        if (administratorRole is not null)
        {
            int activeAdministrators = await userRepository.CountActiveWithRoleAsync(administratorRole.Id, cancellationToken);

            Result lastAdministratorCheck = LastAdministratorRule.CheckSuspension(
                administratorRole, user.HoldsRole(administratorRole.Id), user.IsSuspended, activeAdministrators);

            if (lastAdministratorCheck.IsFailure)
            {
                return lastAdministratorCheck;
            }
        }

        user.Suspend(command.Reason, actorIdResult.Value, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
