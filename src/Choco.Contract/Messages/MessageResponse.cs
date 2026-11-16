namespace Choco.Contract.Messages;

/// <summary>
/// A message in a direct conversation.
/// </summary>
/// <param name="Id">The message's id.</param>
/// <param name="ConversationId">The id of the conversation the message belongs to.</param>
/// <param name="Sequence">The message's position in its conversation, starting at 1 and growing by one per message. Order messages by it, never by time.</param>
/// <param name="ClientMessageId">The GUID the sender's client created for the send.</param>
/// <param name="SenderId">The id of the user who sent the message.</param>
/// <param name="Content">The message text, without leading or trailing whitespace.</param>
/// <param name="CreatedAt">When the message was saved, in UTC.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "conversationId": "00000000-0000-0000-0000-000000000002", "sequence": 1, "clientMessageId": "00000000-0000-0000-0000-000000000004", "senderId": "00000000-0000-0000-0000-000000000003", "content": "content", "createdAt": "2026-01-01T00:00:00+00:00" }</example>
public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    long Sequence,
    Guid ClientMessageId,
    Guid SenderId,
    string Content,
    DateTimeOffset CreatedAt);
