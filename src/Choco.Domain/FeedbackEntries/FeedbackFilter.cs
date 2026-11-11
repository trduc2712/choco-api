namespace Choco.Domain.FeedbackEntries;

public sealed record FeedbackFilter(
    FeedbackStatus? Status,
    FeedbackCategory? Category,
    IReadOnlyList<string> MessageTerms,
    string? Author);
