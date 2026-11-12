using Choco.Domain.Users;

namespace Choco.Application.Users.GetAdminUsers;

internal static class UserStates
{
    public const string Options = "active, deleted";

    public static bool TryParse(string? value, out UserState state)
    {
        state = default;

        switch (value)
        {
            case "active":
                state = UserState.Active;
                return true;
            case "deleted":
                state = UserState.Deleted;
                return true;
            default:
                return false;
        }
    }
}
