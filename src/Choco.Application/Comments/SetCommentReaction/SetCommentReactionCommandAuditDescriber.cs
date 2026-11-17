using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Comments;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.SetCommentReaction;

internal sealed class SetCommentReactionCommandAuditDescriber(ICommentRepository commentRepository)
    : IAuditDescriber<SetCommentReactionCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        SetCommentReactionCommand request, Result response, CancellationToken cancellationToken)
    {
        Comment? comment = await commentRepository.GetByIdIncludingDeletedForReadAsync(request.CommentId, cancellationToken);

        return new AuditDescription(
            AuditAction.CommentReacted,
            AuditTargetType.Comment,
            request.CommentId,
            comment?.AuthorId,
            $"Type: {request.Type}");
    }
}
