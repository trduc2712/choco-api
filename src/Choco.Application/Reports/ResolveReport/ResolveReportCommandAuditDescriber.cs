using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Reports;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandAuditDescriber(
    IReportRepository reportRepository,
    IPostRepository postRepository,
    IUserRepository userRepository) : IAuditDescriber<ResolveReportCommand, Result>
{
    private AuditDescription? _sideEffect;

    public async Task CaptureAsync(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        Report? report = await reportRepository.GetByIdForReadAsync(request.ReportId, cancellationToken);

        if (report is null || report.Status == ReportStatus.Resolved)
        {
            return;
        }

        ReportEnums.TryParse(request.Outcome, out ReportOutcome outcome);

        _sideEffect = outcome switch
        {
            ReportOutcome.ContentRemoved => await DescribePostRemovalAsync(report, cancellationToken),
            ReportOutcome.UserSuspended => await DescribeSuspensionAsync(report, cancellationToken),
            _ => null
        };
    }

    public async Task<AuditDescription> DescribeAsync(
        ResolveReportCommand request, Result response, CancellationToken cancellationToken)
    {
        Report? report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);

        Guid? affectedUserId = report?.TargetUserId;

        if (affectedUserId is null && report?.TargetPostId is { } postId)
        {
            Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(postId, cancellationToken);
            affectedUserId = post?.AuthorId;
        }

        return new AuditDescription(
            AuditAction.ReportResolved, AuditTargetType.Report, request.ReportId, affectedUserId, $"Outcome: {request.Outcome}");
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        ResolveReportCommand request, Result response, CancellationToken cancellationToken)
    {
        AuditDescription resolved = await DescribeAsync(request, response, cancellationToken);

        return _sideEffect is null ? [resolved] : [resolved, _sideEffect];
    }

    private async Task<AuditDescription?> DescribePostRemovalAsync(Report report, CancellationToken cancellationToken)
    {
        if (report.TargetPostId is not { } postId)
        {
            return null;
        }

        Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(postId, cancellationToken);

        return post is { IsDeleted: false }
            ? new AuditDescription(AuditAction.PostDeleted, AuditTargetType.Post, post.Id, post.AuthorId)
            : null;
    }

    private async Task<AuditDescription?> DescribeSuspensionAsync(Report report, CancellationToken cancellationToken)
    {
        Guid? userId = report.TargetUserId;

        if (userId is null && report.TargetPostId is { } postId)
        {
            Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(postId, cancellationToken);
            userId = post?.AuthorId;
        }

        User? user = userId is { } id ? await userRepository.GetByIdForReadAsync(id, cancellationToken) : null;

        return user is { IsSuspended: false }
            ? new AuditDescription(
                AuditAction.UserSuspended, AuditTargetType.User, user.Id, user.Id, $"Reason: {report.SuspensionReason}")
            : null;
    }
}
