using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

/// <summary>Lists the caller's conversations by most recent activity.</summary>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size from 1 to 50. Defaults to 20.</param>
/// <example>{"cursor":null,"limit":20}</example>
public sealed record GetConversationsQuery(string? Cursor, int Limit) : IQuery<PageResponse<ConversationResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
