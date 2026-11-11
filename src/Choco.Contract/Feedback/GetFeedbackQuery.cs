using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Lists feedback, newest first.</summary>
/// <param name="Status">Only feedback in this state: new, in_progress, resolved or wont_fix. Null for any state.</param>
/// <param name="Category">Only feedback of this kind: bug, suggestion or other. Null for any kind.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{"status":"new","category":"bug","cursor":null,"limit":20}</example>
public sealed record GetFeedbackQuery(string? Status, string? Category, string? Cursor, int Limit)
    : IQuery<PageResponse<FeedbackResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
