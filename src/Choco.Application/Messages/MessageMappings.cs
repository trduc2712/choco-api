using Choco.Contract.Messages;
using Choco.Domain.Conversations;

namespace Choco.Application.Messages;

internal static class MessageMappings
{
    public static MessageResponse ToResponse(this Message message)
    {
        return new MessageResponse(
            message.Id,
            message.ConversationId,
            message.Sequence,
            message.ClientMessageId,
            message.SenderId,
            message.Content,
            message.SentAt);
    }
}
