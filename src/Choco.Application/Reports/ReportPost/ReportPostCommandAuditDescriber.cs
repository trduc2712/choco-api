using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Reports;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ReportPost;

internal sealed class ReportPostCommandAuditDescriber(IPostRepository postRepository)
    : IAuditDescriber<ReportPostCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        ReportPostCommand request, Result response, CancellationToken cancellationToken)
    {
        Post? post = await postRepository.GetByIdForReadAsync(request.PostId, cancellationToken);

        return new AuditDescription(
            AuditAction.PostReported, AuditTargetType.Post, request.PostId, post?.AuthorId, $"Reason: {request.Reason}");
    }
}
