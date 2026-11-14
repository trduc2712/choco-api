namespace Choco.Contract.Feedback;

/// <summary>A piece of feedback as its sender sees it.</summary>
/// <param name="Id">The feedback's id.</param>
/// <param name="Category">What kind of feedback it is: bug, suggestion or other.</param>
/// <param name="Message">What the user wrote.</param>
/// <param name="Status">Where the team stands on it: new, in_progress, resolved or wont_fix.</param>
/// <param name="CreatedAt">When the feedback was received, in UTC.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","category":"bug","message":"message of the feedback","status":"new","createdAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record MyFeedbackResponse(
    Guid Id,
    string Category,
    string Message,
    string Status,
    DateTimeOffset CreatedAt);
