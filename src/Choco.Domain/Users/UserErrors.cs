using Choco.SharedKernel.Results;

namespace Choco.Domain.Users;

public static class UserErrors
{
    public static readonly Error UsernameTaken = Error.Conflict("User.UsernameTaken", "The username is unavailable.");
    public static readonly Error Deleted = Error.Unauthorized("User.Deleted", "The user has been deleted.");
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "The user was not found.");
}
