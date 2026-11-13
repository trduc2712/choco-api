using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.LikePost;

internal sealed class LikePostCommandAuditDescriber(IPostRepository postRepository)
    : IAuditDescriber<LikePostCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        LikePostCommand request, Result response, CancellationToken cancellationToken)
    {
        Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(request.PostId, cancellationToken);

        return new AuditDescription(AuditAction.PostLiked, AuditTargetType.Post, request.PostId, post?.AuthorId);
    }
}
