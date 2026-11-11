namespace Choco.Contract.Feedback;

/// <summary>The feedback that was just received.</summary>
/// <param name="Id">The feedback's id.</param>
/// <param name="CreatedAt">When the feedback was received, in UTC.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","createdAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record CreatedFeedbackResponse(Guid Id, DateTimeOffset CreatedAt);
