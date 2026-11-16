using Choco.Domain.Users;

namespace Choco.Domain.Conversations;

public sealed record ConversationEntry
{
    public required Conversation Conversation { get; init; }
    public required User OtherUser { get; init; }
    public Message? LastMessage { get; init; }
    public bool IsBlockedByMe { get; init; }
}
