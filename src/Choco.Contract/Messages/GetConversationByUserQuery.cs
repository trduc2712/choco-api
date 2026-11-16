using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

/// <summary>Finds the caller's conversation with another user.</summary>
/// <param name="UserId">The other user's identifier.</param>
/// <example>{"userId":"00000000-0000-0000-0000-000000000002"}</example>
public sealed record GetConversationByUserQuery(Guid UserId) : IQuery<ConversationResponse>;
