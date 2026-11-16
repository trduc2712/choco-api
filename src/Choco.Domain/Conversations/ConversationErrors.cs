using Choco.SharedKernel.Results;

namespace Choco.Domain.Conversations;

public static class ConversationErrors
{
    public static readonly Error SelfConversation = Error.Validation(
        "Conversation.SelfConversation", "Users cannot send messages to themselves.");
}
