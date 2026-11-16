namespace Choco.Contract.Messages;

/// <summary>A direct conversation as seen by the caller.</summary>
/// <param name="Id">The conversation identifier.</param>
/// <param name="OtherUser">The other participant.</param>
/// <param name="LastMessage">The newest message, or null when the conversation is empty.</param>
/// <param name="UnreadCount">The number of messages after the caller's read position.</param>
/// <param name="LastReadSequence">The caller's read position, or zero before any messages are read.</param>
/// <param name="IsBlockedByMe">Whether the caller has blocked the other participant.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","otherUser":{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":null},"lastMessage":null,"unreadCount":0,"lastReadSequence":0,"isBlockedByMe":false}</example>
public sealed record ConversationResponse(
    Guid Id,
    MessagingUserResponse OtherUser,
    MessageResponse? LastMessage,
    long UnreadCount,
    long LastReadSequence,
    bool IsBlockedByMe);
