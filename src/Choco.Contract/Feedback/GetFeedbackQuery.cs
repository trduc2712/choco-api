using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Lists feedback, newest first.</summary>
/// <param name="Status">Only feedback in this state: new, in_progress, resolved or wont_fix. Null for any state.</param>
/// <param name="Category">Only feedback of this kind: bug, suggestion or other. Null for any kind.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{"status":"new","category":"bug","page":1,"pageSize":20}</example>
public sealed record GetFeedbackQuery(string? Status, string? Category, int Page, int PageSize)
    : IQuery<OffsetPageResponse<FeedbackResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}
