using Choco.Application.Abstractions.Messaging;
using Choco.Application.Content;
using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.GetAdminReports;

internal sealed class GetAdminReportsQueryHandler(
    IReportRepository reportRepository,
    IPostRepository postRepository,
    IUserRepository userRepository)
    : IQueryHandler<GetAdminReportsQuery, OffsetPageResponse<AdminReportResponse>>
{
    public async Task<Result<OffsetPageResponse<AdminReportResponse>>> Handle(
        GetAdminReportsQuery query, CancellationToken cancellationToken)
    {
        ReportListFilter filter = new(
            ReportEnums.TryParse(query.TargetType, out ReportTargetType targetType) ? targetType : null,
            query.TargetId,
            ReportEnums.TryParse(query.Status, out ReportStatus status) ? status : null,
            ReportEnums.TryParse(query.Reason, out ReportReason reason) ? reason : null,
            ReportEnums.TryParse(query.Outcome, out ReportOutcome outcome) ? outcome : null,
            ReportSearches.Create(query.Q, GetAdminReportsQuery.MaxSearchWords),
            query.From,
            query.To);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await reportRepository.CountForReadAsync(filter, cancellationToken);
        IReadOnlyList<Report> reports = skip >= totalCount
            ? []
            : await reportRepository.ListForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        List<Guid> postIds = reports
            .Select(report => report.TargetPostId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        List<Post> posts = postIds.Count == 0
            ? []
            : (await postRepository.ListByIdsForReadAsync(postIds, cancellationToken)).ToList();
        Dictionary<Guid, Post> postsById = posts.ToDictionary(post => post.Id);

        List<Guid> userIds = reports
            .SelectMany(report => new[] { report.ReporterId, report.ResolvedById, report.TargetUserId })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        Dictionary<Guid, User> users = userIds.Count == 0
            ? []
            : (await userRepository.ListByIdsForReadAsync(userIds, cancellationToken))
                .ToDictionary(user => user.Id);

        Dictionary<Guid, string> authorNames = await ContentAuthors.LoadUsernamesAsync(
            userRepository, posts.Select(post => post.AuthorId), cancellationToken);

        List<AdminReportResponse> items = reports
            .Select(report => new AdminReportResponse(
                report.Id,
                report.TargetType.ToString(),
                (report.TargetPostId ?? report.TargetUserId)!.Value,
                report.Reason.ToString(),
                report.Details,
                ToUser(report.ReporterId, users)!,
                report.CreatedAt,
                report.Status.ToString(),
                report.Outcome?.ToString(),
                ToUser(report.ResolvedById, users),
                report.ResolvedAt,
                ToPost(report.TargetPostId, postsById, authorNames),
                ToTargetUser(report.TargetUserId, users)))
            .ToList();

        return Result<OffsetPageResponse<AdminReportResponse>>.Success(
            new OffsetPageResponse<AdminReportResponse>(items, query.Page, query.PageSize, totalCount));
    }

    private static ReportUserResponse? ToUser(Guid? userId, Dictionary<Guid, User> users)
    {
        return userId is { } id ? new ReportUserResponse(id, users.GetValueOrDefault(id)?.Username) : null;
    }

    private static ReportedPostResponse? ToPost(
        Guid? postId, Dictionary<Guid, Post> posts, Dictionary<Guid, string> authorNames)
    {
        return postId is { } id && posts.TryGetValue(id, out Post? post)
            ? new ReportedPostResponse(post.Content, ContentAuthors.ToResponse(post.AuthorId, authorNames))
            : null;
    }

    private static ReportedUserResponse? ToTargetUser(Guid? userId, Dictionary<Guid, User> users)
    {
        return userId is { } id && users.TryGetValue(id, out User? user)
            ? new ReportedUserResponse(user.Username, user.DisplayName, user.IsSuspended)
            : null;
    }
}
