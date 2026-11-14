namespace Choco.Contract.Feedback;

/// <summary>A piece of feedback as it appears in the administrator's list.</summary>
/// <param name="Id">The feedback's id.</param>
/// <param name="Author">The user who sent it, or null when that account is deleted.</param>
/// <param name="Category">What kind of feedback it is: bug, suggestion or other.</param>
/// <param name="MessagePreview">The start of what the user wrote, up to 200 characters. A longer message is cut and ends with an ellipsis; get the feedback by id for the whole message and its context.</param>
/// <param name="Status">The triage state: new, in_progress, resolved or wont_fix.</param>
/// <param name="CreatedAt">When the feedback was received, in UTC.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","author":{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name"},"category":"bug","messagePreview":"message of the feedback","status":"new","createdAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record FeedbackSummaryResponse(
    Guid Id,
    FeedbackAuthorResponse? Author,
    string Category,
    string MessagePreview,
    string Status,
    DateTimeOffset CreatedAt)
{
    public const int MessagePreviewMaxLength = 200;
}
