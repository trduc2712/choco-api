using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

/// <summary>
/// A text message to send to another user.
/// </summary>
/// <param name="RecipientId">Ignored; the recipient is the user named in the route.</param>
/// <param name="ClientMessageId">A GUID the client creates once for this message and sends again unchanged when it retries the send. Required.</param>
/// <param name="Content">The message text. Required, at most 2000 characters once leading and trailing whitespace is removed.</param>
/// <example>{ "clientMessageId": "00000000-0000-0000-0000-000000000004", "content": "content" }</example>
public sealed record SendMessageCommand(Guid RecipientId, Guid ClientMessageId, string Content)
    : ICommand<MessageResponse>;
