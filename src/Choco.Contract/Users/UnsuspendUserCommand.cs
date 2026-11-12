using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record UnsuspendUserCommand(Guid UserId) : ICommand;
