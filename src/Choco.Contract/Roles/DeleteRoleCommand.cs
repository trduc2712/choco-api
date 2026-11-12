using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

public sealed record DeleteRoleCommand(Guid Id) : ICommand<RoleResponse>;
