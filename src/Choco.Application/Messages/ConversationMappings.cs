using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.Domain.Users;

namespace Choco.Application.Messages;

internal static class ConversationMappings
{
    public static ConversationResponse ToResponse(this ConversationEntry entry, Guid userId)
    {
        User other = entry.OtherUser;
        return new ConversationResponse(
            entry.Conversation.Id,
            new MessagingUserResponse(other.Id, other.Username, other.DisplayName, other.AvatarUrl),
            entry.LastMessage?.ToResponse(),
            entry.Conversation.UnreadCountFor(userId),
            entry.Conversation.LastReadSequenceOf(userId),
            entry.IsBlockedByMe);
    }
}
