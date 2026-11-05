using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record AssignRoleCommand(Guid UserId, Guid RoleId) : ICommand;
