using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Roles;

public static class LastAdministratorRule
{
    public static Result Check(Role role, bool userHoldsRole, int holderCount)
    {
        ArgumentNullException.ThrowIfNull(role);

        return role.SystemKey == SystemRoles.Admin && userHoldsRole && holderCount <= 1
            ? Result.Failure(RoleErrors.LastAdministrator)
            : Result.Success();
    }

    public static Result CheckSuspension(Role role, bool userHoldsRole, bool userIsSuspended, int activeHolderCount)
    {
        ArgumentNullException.ThrowIfNull(role);

        return role.SystemKey == SystemRoles.Admin && userHoldsRole && !userIsSuspended && activeHolderCount <= 1
            ? Result.Failure(UserErrors.LastAdministrator)
            : Result.Success();
    }
}
