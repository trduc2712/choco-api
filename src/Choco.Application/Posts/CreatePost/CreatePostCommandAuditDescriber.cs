using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.CreatePost;

internal sealed class CreatePostCommandAuditDescriber : IAuditDescriber<CreatePostCommand, Result<PostResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        CreatePostCommand request, Result<PostResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.PostCreated, AuditTargetType.Post, response.Value.Id, response.Value.AuthorId));
    }
}
