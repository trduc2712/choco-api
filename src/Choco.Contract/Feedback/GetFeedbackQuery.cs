using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Lists feedback, newest first.</summary>
/// <param name="Status">Only feedback in this state: new, in_progress, resolved or wont_fix. Null for any state.</param>
/// <param name="Category">Only feedback of this kind: bug, suggestion or other. Null for any kind.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="Q">Words that must all appear in the message. Null or blank for no filter.</param>
/// <param name="Author">Part of the sender's username or display name. Null or blank for no filter.</param>
/// <example>{"status":"new","category":"bug","page":1,"pageSize":20,"q":"login error","author":"anh"}</example>
public sealed record GetFeedbackQuery(
    string? Status, string? Category, int Page, int PageSize, string? Q = null, string? Author = null)
    : IQuery<OffsetPageResponse<FeedbackResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int SearchMaxLength = 100;
    public const int MaxSearchTerms = 5;
}
