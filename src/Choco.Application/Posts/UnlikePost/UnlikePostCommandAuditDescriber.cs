using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.UnlikePost;

internal sealed class UnlikePostCommandAuditDescriber(IPostRepository postRepository)
    : IAuditDescriber<UnlikePostCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        UnlikePostCommand request, Result response, CancellationToken cancellationToken)
    {
        Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(request.PostId, cancellationToken);

        return new AuditDescription(AuditAction.PostUnliked, AuditTargetType.Post, request.PostId, post?.AuthorId);
    }
}
