using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Messages;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.SendMessage;

internal sealed class SendMessageCommandAuditDescriber : IAuditDescriber<SendMessageCommand, Result<MessageResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        SendMessageCommand request, Result<MessageResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.MessageSent,
            AuditTargetType.Message,
            response.Value.Id,
            response.Value.SenderId,
            $"ConversationId: {response.Value.ConversationId}"));
    }
}
