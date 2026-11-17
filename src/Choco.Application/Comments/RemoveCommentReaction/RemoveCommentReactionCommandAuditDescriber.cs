using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Comments;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.RemoveCommentReaction;

internal sealed class RemoveCommentReactionCommandAuditDescriber(ICommentRepository commentRepository)
    : IAuditDescriber<RemoveCommentReactionCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        RemoveCommentReactionCommand request, Result response, CancellationToken cancellationToken)
    {
        Comment? comment = await commentRepository.GetByIdIncludingDeletedForReadAsync(request.CommentId, cancellationToken);

        return new AuditDescription(
            AuditAction.CommentReactionRemoved,
            AuditTargetType.Comment,
            request.CommentId,
            comment?.AuthorId);
    }
}
