using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Lists individual reports for moderators, newest first.</summary>
/// <param name="TargetType">Only reports about this kind of target: Post or User. Null for both.</param>
/// <param name="TargetId">Only reports about the post or user with this id. Null for any target.</param>
/// <param name="Status">Only reports in this status: Open or Resolved. Null for both.</param>
/// <param name="Reason">Only reports that give this reason: Spam, Harassment, HateSpeech, Violence or Other. Null for any reason.</param>
/// <param name="Outcome">Only resolved reports with this outcome: Dismissed, ContentRemoved or UserSuspended. Null for any outcome.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="From">Only reports made at or after this time. Null for no lower limit.</param>
/// <param name="To">Only reports made before this time. Null for no upper limit.</param>
/// <param name="Q">Searches the reports: part of the username or display name of a reported user, or of the author of a reported post, taken as one phrase; words that must all appear in a reported post; or part of the reporter's username. Null or blank for no filter.</param>
/// <example>{"targetType":"Post","targetId":"00000000-0000-0000-0000-000000000001","status":"Resolved","reason":"Spam","outcome":"ContentRemoved","page":1,"pageSize":20,"from":"2026-01-01T00:00:00+00:00","to":null,"q":"hello world"}</example>
public sealed record GetAdminReportsQuery(
    string? TargetType,
    Guid? TargetId,
    string? Status,
    string? Reason,
    string? Outcome,
    int Page,
    int PageSize,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Q = null) : IQuery<OffsetPageResponse<AdminReportResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int SearchMaxLength = 100;
    public const int MaxSearchWords = 5;
}
