using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

public sealed record DeletePostCommand(Guid Id) : ICommand;
