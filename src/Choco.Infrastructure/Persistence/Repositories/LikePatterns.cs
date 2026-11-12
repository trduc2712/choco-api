namespace Choco.Infrastructure.Persistence.Repositories;

internal static class LikePatterns
{
    public const string Escape = "\\";

    public static string Contains(string text)
    {
        string escaped = text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
