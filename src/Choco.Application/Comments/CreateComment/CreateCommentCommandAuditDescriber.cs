using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Comments;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.CreateComment;

internal sealed class CreateCommentCommandAuditDescriber : IAuditDescriber<CreateCommentCommand, Result<CommentResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        CreateCommentCommand request, Result<CommentResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.CommentCreated,
            AuditTargetType.Comment,
            response.Value.Id,
            response.Value.AuthorId,
            $"PostId: {response.Value.PostId}"));
    }
}
