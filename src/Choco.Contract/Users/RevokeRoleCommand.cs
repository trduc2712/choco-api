using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record RevokeRoleCommand(Guid UserId, Guid RoleId) : ICommand;
