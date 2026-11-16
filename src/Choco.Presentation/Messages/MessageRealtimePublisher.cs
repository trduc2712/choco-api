using Choco.Application.Abstractions.Messages;
using Choco.Contract.Messages;

using Microsoft.AspNetCore.SignalR;

namespace Choco.Presentation.Messages;

internal sealed class MessageRealtimePublisher(IHubContext<MessagesHub> hubContext) : IMessageRealtimePublisher
{
    public Task PublishAsync(
        string senderIdentityId, string recipientIdentityId, MessageResponse message, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Users(senderIdentityId, recipientIdentityId)
            .SendAsync(MessagesHub.MessageReceivedEvent, message, cancellationToken);
    }
}
