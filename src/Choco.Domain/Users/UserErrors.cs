using Choco.SharedKernel.Results;

namespace Choco.Domain.Users;

public static class UserErrors
{
    public static readonly Error UsernameTaken = Error.Conflict("User.UsernameTaken", "The username is unavailable.");
    public static readonly Error Deleted = Error.Unauthorized("User.Deleted", "The user has been deleted.");
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "The user was not found.");

    public static readonly Error CannotSuspendSelf = Error.Validation(
        "User.CannotSuspendSelf", "You cannot suspend yourself.");

    public static readonly Error LastAdministrator = Error.Conflict(
        "User.LastAdministrator", "The last administrator who is not suspended cannot be suspended.");

    public static Error Suspended(string reason, DateTimeOffset suspendedAt)
    {
        return new ErrorWithExtensions(
            "User.Suspended",
            $"Your account is suspended: {reason}",
            ErrorType.Forbidden,
            new Dictionary<string, object?>
            {
                ["suspension"] = new Dictionary<string, object?>
                {
                    ["reason"] = reason,
                    ["suspendedAt"] = suspendedAt
                }
            });
    }
}
