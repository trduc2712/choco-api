using Choco.SharedKernel.Results;

namespace Choco.Domain.Roles;

public static class RoleErrors
{
    public static readonly Error NotFound = Error.NotFound("Role.NotFound", "The role was not found.");

    public static readonly Error DefaultNotAssignable = Error.Validation(
        "Role.DefaultNotAssignable", "The default role belongs to every signed-in user and cannot be assigned or revoked.");

    public static readonly Error NameTaken = Error.Conflict(
        "Role.NameTaken", "A role with this name already exists.");

    public static readonly Error LastAdministrator = Error.Conflict(
        "Role.LastAdministrator", "The last administrator cannot lose the administrator role.");
}
