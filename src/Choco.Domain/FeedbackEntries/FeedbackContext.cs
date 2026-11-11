namespace Choco.Domain.FeedbackEntries;

public sealed record FeedbackContext(
    string Path,
    FeedbackLocale Locale,
    FeedbackTheme Theme,
    string UserAgent,
    int ViewportWidth,
    int ViewportHeight,
    string AppVersion)
{
    public const int PathMaxLength = 2048;
    public const int UserAgentMaxLength = 512;
    public const int AppVersionMaxLength = 64;
}
