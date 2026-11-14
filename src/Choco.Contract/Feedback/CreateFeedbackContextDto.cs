namespace Choco.Contract.Feedback;

/// <summary>Where and how the user was using the app when they sent the feedback. The browser's user agent is not part of it; the server takes it from the User-Agent header.</summary>
/// <param name="Path">The page's pathname, starting with a slash and without query string or hash, up to 2048 characters.</param>
/// <param name="Locale">The language of the app: en, ja or vi.</param>
/// <param name="Theme">The theme of the app: light, dark or system.</param>
/// <param name="Viewport">The size of the browser viewport.</param>
/// <param name="AppVersion">The build commit SHA of the web app, up to 64 characters.</param>
/// <example>{"path":"/posts/123","locale":"en","theme":"system","viewport":{"width":1280,"height":720},"appVersion":"app-version"}</example>
public sealed record CreateFeedbackContextDto(
    string Path,
    string Locale,
    string Theme,
    FeedbackViewportDto Viewport,
    string AppVersion);
