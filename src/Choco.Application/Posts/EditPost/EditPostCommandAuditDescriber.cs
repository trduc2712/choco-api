using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.EditPost;

internal sealed class EditPostCommandAuditDescriber(AuditChanges auditChanges) : IAuditDescriber<EditPostCommand, Result<PostResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        EditPostCommand request, Result<PostResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.PostEdited, AuditTargetType.Post, response.Value.Id, response.Value.AuthorId,
            Changes: auditChanges.Get(response)));
    }
}
