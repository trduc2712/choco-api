using Choco.Contract.Messages;

namespace Choco.Application.Abstractions.Messages;

public interface IMessageRealtimePublisher
{
    Task PublishAsync(
        string senderIdentityId, string recipientIdentityId, MessageResponse message, CancellationToken cancellationToken);
}
