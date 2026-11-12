namespace Choco.Contract.Reports;

/// <summary>A post or a user that has open reports, one row of the moderation queue.</summary>
/// <param name="TargetType">What was reported: Post or User.</param>
/// <param name="TargetPostId">The reported post's id; null unless the target is a post.</param>
/// <param name="TargetUserId">The reported user's id; null unless the target is a user.</param>
/// <param name="OpenReportCount">How many open reports are about this target.</param>
/// <param name="Reasons">The reasons given by the open reports with how many gave each, the most given first.</param>
/// <param name="OldestReportedAt">When the oldest open report about this target was made, in UTC.</param>
/// <param name="ReportId">The id of the oldest open report. Resolve this report to resolve every open report about the target.</param>
/// <param name="Post">The reported post and its author; null unless the target is a post.</param>
/// <param name="User">The reported user; null unless the target is a user.</param>
/// <param name="Reports">The oldest open reports about the target, at most 10, oldest first. OpenReportCount says how many there are in all.</param>
/// <example>{"targetType":"Post","targetPostId":"00000000-0000-0000-0000-000000000002","targetUserId":null,"openReportCount":2,"reasons":[{"reason":"Spam","count":2}],"oldestReportedAt":"2026-01-01T00:00:00+00:00","reportId":"00000000-0000-0000-0000-000000000001","post":{"content":"content","author":{"id":"00000000-0000-0000-0000-000000000004","username":"username-2"}},"user":null,"reports":[{"id":"00000000-0000-0000-0000-000000000001","reason":"Spam","details":"details","reporterId":"00000000-0000-0000-0000-000000000003","reporterUsername":"username","createdAt":"2026-01-01T00:00:00+00:00"}]}</example>
public sealed record ReportQueueItemResponse(
    string TargetType,
    Guid? TargetPostId,
    Guid? TargetUserId,
    int OpenReportCount,
    IReadOnlyList<ReportReasonCountResponse> Reasons,
    DateTimeOffset OldestReportedAt,
    Guid ReportId,
    ReportedPostResponse? Post,
    ReportedUserResponse? User,
    IReadOnlyList<ReportDetailResponse> Reports);
