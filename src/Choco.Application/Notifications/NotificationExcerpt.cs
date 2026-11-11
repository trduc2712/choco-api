namespace Choco.Application.Notifications;

internal static class NotificationExcerpt
{
    public const int MaxLength = 140;

    private const string Ellipsis = "…";

    public static string? Of(string? content)
    {
        if (content is null)
        {
            return null;
        }

        string normalized = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= MaxLength)
        {
            return normalized;
        }

        int length = MaxLength - Ellipsis.Length;
        if (char.IsHighSurrogate(normalized[length - 1]))
        {
            length--;
        }

        return normalized[..length].TrimEnd() + Ellipsis;
    }
}
