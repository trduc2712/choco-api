namespace Choco.Contract.Feedback;

/// <summary>A piece of feedback as an administrator sees it.</summary>
/// <param name="Id">The feedback's id.</param>
/// <param name="Author">The user who sent it, or null when that account is deleted.</param>
/// <param name="Category">What kind of feedback it is: bug, suggestion or other.</param>
/// <param name="Message">What the user wrote.</param>
/// <param name="Context">Where and how the user was using the app.</param>
/// <param name="Status">The triage state: new, in_progress, resolved or wont_fix.</param>
/// <param name="CreatedAt">When the feedback was received, in UTC.</param>
/// <param name="UpdatedAt">When the feedback last changed, in UTC.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","author":{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"},"category":"bug","message":"message of the feedback","context":{"path":"/posts/123","locale":"en","theme":"system","userAgent":"user-agent","viewport":{"width":1280,"height":720},"appVersion":"app-version"},"status":"new","createdAt":"2026-01-01T00:00:00+00:00","updatedAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record FeedbackResponse(
    Guid Id,
    FeedbackAuthorResponse? Author,
    string Category,
    string Message,
    FeedbackContextDto Context,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
