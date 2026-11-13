using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Feedback;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.CreateFeedback;

internal sealed class CreateFeedbackCommandAuditDescriber
    : IAuditDescriber<CreateFeedbackCommand, Result<CreatedFeedbackResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        CreateFeedbackCommand request, Result<CreatedFeedbackResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.FeedbackSent, AuditTargetType.Feedback, response.Value.Id));
    }
}
