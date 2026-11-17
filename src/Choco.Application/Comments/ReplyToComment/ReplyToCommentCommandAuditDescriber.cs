using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Comments;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.ReplyToComment;

internal sealed class ReplyToCommentCommandAuditDescriber : IAuditDescriber<ReplyToCommentCommand, Result<CommentResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        ReplyToCommentCommand request, Result<CommentResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.CommentCreated,
            AuditTargetType.Comment,
            response.Value.Id,
            response.Value.AuthorId,
            $"PostId: {response.Value.PostId}, ParentId: {response.Value.ParentId}"));
    }
}
