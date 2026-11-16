using System.Text.Json.Serialization;

using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

/// <summary>Advances the caller's read position in a conversation.</summary>
/// <param name="Sequence">The last sequence read, zero or greater.</param>
/// <example>{"sequence":12}</example>
public sealed record MarkConversationReadCommand(long Sequence) : ICommand
{
    [JsonIgnore]
    public Guid ConversationId { get; init; }
}
