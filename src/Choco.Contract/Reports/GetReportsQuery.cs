using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Lists the posts and users that have open reports, oldest first.</summary>
/// <param name="TargetType">Only targets of this kind: Post or User. Null for both.</param>
/// <param name="Reason">Only targets with at least one open report that gives this reason: Spam, Harassment, HateSpeech, Violence or Other. Null for any reason.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="Q">Searches the reported targets: part of a reported user's username or display name, or of the username or display name of the author of a reported post, taken as one phrase; or words that must all appear in a reported post. Null or blank for no filter.</param>
/// <example>{ "targetType": "Post", "reason": "Spam", "page": 1, "pageSize": 20, "q": "hello world" }</example>
public sealed record GetReportsQuery(string? TargetType, string? Reason, int Page, int PageSize, string? Q = null)
    : IQuery<OffsetPageResponse<ReportQueueItemResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxReportsPerItem = 10;
    public const int SearchMaxLength = 100;
    public const int MaxSearchWords = 5;
}
