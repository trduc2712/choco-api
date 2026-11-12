namespace Choco.Application.Content;

internal static class SearchTerms
{
    public static string[] Split(string? text, int maxTerms)
    {
        return (text ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(maxTerms)
            .ToArray();
    }
}
