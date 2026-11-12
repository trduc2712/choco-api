using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users;

internal sealed class UserSuspender(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> SuspendAsync(
        User user, Guid actorId, string reason, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> actorPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(actorId, cancellationToken);
        IReadOnlyCollection<string> userPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(user.Id, cancellationToken);

        Result check = user.CheckCanBeSuspendedBy(actorId, actorPermissions, userPermissions);

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

        user.Suspend(reason, actorId, timeProvider.GetUtcNow());

        return Result.Success();
    }
}
