using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.DeletePost;

internal sealed class DeletePostCommandAuditDescriber(IPostRepository postRepository)
    : IAuditDescriber<DeletePostCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        DeletePostCommand request, Result response, CancellationToken cancellationToken)
    {
        Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(request.Id, cancellationToken);

        return new AuditDescription(AuditAction.PostDeleted, AuditTargetType.Post, request.Id, post?.AuthorId);
    }
}
