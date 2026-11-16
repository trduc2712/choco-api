using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Messages;

public sealed record GetMessageQuery(Guid Id) : IQuery<MessageResponse>;
