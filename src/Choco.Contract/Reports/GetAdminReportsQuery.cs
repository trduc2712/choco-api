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
/// <example>{"targetType":"Post","targetId":"00000000-0000-0000-0000-000000000001","status":"Resolved","reason":"Spam","outcome":"ContentRemoved","page":1,"pageSize":20}</example>
public sealed record GetAdminReportsQuery(
    string? TargetType,
    Guid? TargetId,
    string? Status,
    string? Reason,
    string? Outcome,
    int Page,
    int PageSize) : IQuery<OffsetPageResponse<AdminReportResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}
