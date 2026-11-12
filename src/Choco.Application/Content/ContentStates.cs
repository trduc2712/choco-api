using Choco.Domain.Primitives.Entities;

namespace Choco.Application.Content;

internal static class ContentStates
{
    public const string Options = "visible, deleted";

    public static bool TryParse(string? value, out ContentState state)
    {
        state = default;

        switch (value)
        {
            case "visible":
                state = ContentState.Visible;
                return true;
            case "deleted":
                state = ContentState.Deleted;
                return true;
            default:
                return false;
        }
    }
}
