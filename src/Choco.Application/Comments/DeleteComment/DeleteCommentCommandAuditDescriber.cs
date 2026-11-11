using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Comments;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.DeleteComment;

internal sealed class DeleteCommentCommandAuditDescriber(ICommentRepository commentRepository)
    : IAuditDescriber<DeleteCommentCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        DeleteCommentCommand request, Result response, CancellationToken cancellationToken)
    {
        Comment? comment = await commentRepository.GetByIdIncludingDeletedForReadAsync(request.CommentId, cancellationToken);

        return new AuditDescription(
            AuditAction.CommentDeleted,
            AuditTargetType.Comment,
            request.CommentId,
            comment?.AuthorId,
            comment is null ? null : $"PostId: {comment.PostId}");
    }
}
