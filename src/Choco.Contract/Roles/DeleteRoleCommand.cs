using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>Deletes a role and can assign a replacement to its holders.</summary>
/// <param name="Id">Ignored in the body. The route identifies the role to delete.</param>
/// <param name="ReassignTo">Optional replacement role id. It must identify a different role, and cannot identify the default role.</param>
/// <param name="Reason">Optional reason to store on the audit log entry, at most 500 characters.</param>
/// <example>{"reassignTo":"00000000-0000-0000-0000-000000000002","reason":"Role change reason"}</example>
public sealed record DeleteRoleCommand(Guid Id, Guid? ReassignTo = null, string? Reason = null) : ICommand<RoleResponse>;
