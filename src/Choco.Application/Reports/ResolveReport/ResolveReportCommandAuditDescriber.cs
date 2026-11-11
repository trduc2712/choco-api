using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Reports;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandAuditDescriber(
    IReportRepository reportRepository,
    IPostRepository postRepository) : IAuditDescriber<ResolveReportCommand, Result>
{
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
}
