using Choco.SharedKernel.Results;

namespace Choco.Domain.Roles;

public static class PermissionErrors
{
    public static readonly Error Denied = Error.Forbidden("Permission.Denied", "You do not have permission to do this.");
}
