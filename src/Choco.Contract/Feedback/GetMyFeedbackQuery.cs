using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Lists the feedback the caller sent, newest first.</summary>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "cursor": null, "limit": 20 }</example>
public sealed record GetMyFeedbackQuery(string? Cursor, int Limit) : IQuery<PageResponse<MyFeedbackResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
