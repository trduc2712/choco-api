using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryHandler(
    IReportRepository reportRepository,
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IQueryHandler<GetReportsQuery, OffsetPageResponse<ReportQueueItemResponse>>
{
    public async Task<Result<OffsetPageResponse<ReportQueueItemResponse>>> Handle(
        GetReportsQuery query, CancellationToken cancellationToken)
    {
        ReportTargetType? targetType = ReportEnums.TryParse(query.TargetType, out ReportTargetType parsedTargetType)
            ? parsedTargetType
            : null;
        ReportReason? reason = ReportEnums.TryParse(query.Reason, out ReportReason parsedReason) ? parsedReason : null;
        ReportQueueFilter filter = new(targetType, reason);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await reportRepository.CountQueueAsync(filter, cancellationToken);
        IReadOnlyList<ReportQueueGroup> groups = skip >= totalCount
            ? []
            : await reportRepository.ListQueueForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        List<Guid> postIds = groups
            .Where(group => group.TargetType == ReportTargetType.Post)
            .Select(group => group.TargetId)
            .ToList();
        List<Post> posts = postIds.Count == 0
            ? []
            : (await postRepository.ListByIdsForReadAsync(postIds, cancellationToken)).ToList();
        Dictionary<Guid, Post> postsById = posts.ToDictionary(post => post.Id);

        List<Guid> targetUserIds = groups
            .Where(group => group.TargetType == ReportTargetType.User)
            .Select(group => group.TargetId)
            .ToList();
        Dictionary<Guid, User> targetUsers = targetUserIds.Count == 0
            ? []
            : (await userRepository.ListByIdsForReadAsync(targetUserIds, cancellationToken))
                .ToDictionary(user => user.Id);

        List<Guid> authorIds = posts.Select(post => post.AuthorId).Distinct().ToList();
        Dictionary<Guid, string> authorNames = authorIds.Count == 0
            ? []
            : (await userRepository.ListByIdsIncludingDeletedForReadAsync(authorIds, cancellationToken))
                .ToDictionary(user => user.Id, user => user.Username);

        List<Guid> reporterIds = groups
            .SelectMany(group => group.OpenReports.Take(GetReportsQuery.MaxReportsPerItem))
            .Select(report => report.ReporterId)
            .Distinct()
            .ToList();
        Dictionary<Guid, string> reporterNames = reporterIds.Count == 0
            ? []
            : (await userRepository.ListByIdsForReadAsync(reporterIds, cancellationToken))
                .ToDictionary(user => user.Id, user => user.Username);

        List<ReportQueueItemResponse> items = groups
            .Select(group => ToItem(group, postsById, targetUsers, authorNames, reporterNames))
            .ToList();

        return Result<OffsetPageResponse<ReportQueueItemResponse>>.Success(
            new OffsetPageResponse<ReportQueueItemResponse>(items, query.Page, query.PageSize, totalCount));
    }

    private static ReportQueueItemResponse ToItem(
        ReportQueueGroup group,
        Dictionary<Guid, Post> posts,
        Dictionary<Guid, User> users,
        Dictionary<Guid, string> authorNames,
        Dictionary<Guid, string> reporterNames)
    {
        Report oldest = group.OpenReports[0];
        bool isPost = group.TargetType == ReportTargetType.Post;

        ReportedPostResponse? post = isPost && posts.TryGetValue(group.TargetId, out Post? reportedPost)
            ? new ReportedPostResponse(
                reportedPost.Content,
                new ContentAuthorResponse(reportedPost.AuthorId, authorNames.GetValueOrDefault(reportedPost.AuthorId)))
            : null;

        ReportedUserResponse? user = !isPost && users.TryGetValue(group.TargetId, out User? reportedUser)
            ? new ReportedUserResponse(reportedUser.Username, reportedUser.DisplayName, reportedUser.IsSuspended)
            : null;

        List<ReportReasonCountResponse> reasons = group.OpenReports
            .GroupBy(report => report.Reason)
            .Select(reasonGroup => new ReportReasonCountResponse(reasonGroup.Key.ToString(), reasonGroup.Count()))
            .OrderByDescending(reasonCount => reasonCount.Count)
            .ThenBy(reasonCount => reasonCount.Reason, StringComparer.Ordinal)
            .ToList();

        List<ReportDetailResponse> reports = group.OpenReports
            .Take(GetReportsQuery.MaxReportsPerItem)
            .Select(report => new ReportDetailResponse(
                report.Id,
                report.Reason.ToString(),
                report.Details,
                report.ReporterId,
                reporterNames.GetValueOrDefault(report.ReporterId),
                report.CreatedAt))
            .ToList();

        return new ReportQueueItemResponse(
            group.TargetType.ToString(),
            isPost ? group.TargetId : null,
            isPost ? null : group.TargetId,
            group.OpenReports.Count,
            reasons,
            oldest.CreatedAt,
            oldest.Id,
            post,
            user,
            reports);
    }
}
