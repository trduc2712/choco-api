using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Feedback;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.UpdateFeedbackStatus;

internal sealed class UpdateFeedbackStatusCommandAuditDescriber
    : IAuditDescriber<UpdateFeedbackStatusCommand, Result<FeedbackResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        UpdateFeedbackStatusCommand request, Result<FeedbackResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.FeedbackStatusChanged,
            AuditTargetType.Feedback,
            response.Value.Id,
            response.Value.Author?.Id,
            $"Status: {response.Value.Status}"));
    }
}
