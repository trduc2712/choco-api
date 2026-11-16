using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

/// <summary>Lists a conversation's messages by sequence.</summary>
/// <param name="ConversationId">The conversation identifier.</param>
/// <param name="Cursor">The previous page's nextCursor. Omit when afterSequence is supplied.</param>
/// <param name="AfterSequence">The last sequence the client has, zero or greater. Omit for history.</param>
/// <param name="Limit">The page size from 1 to 50. Defaults to 20.</param>
/// <example>{"conversationId":"00000000-0000-0000-0000-000000000001","cursor":null,"afterSequence":0,"limit":20}</example>
public sealed record GetConversationMessagesQuery(Guid ConversationId, string? Cursor, long? AfterSequence, int Limit)
    : IQuery<PageResponse<MessageResponse>>;
