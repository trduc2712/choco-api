using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;

namespace Choco.Application.Roles;

internal static class RoleAuditChanges
{
    public static IReadOnlyList<AuditChange> Between(RoleResponse? before, RoleResponse? after)
    {
        List<AuditChange> changes = [];

        if (before?.Name != after?.Name)
        {
            changes.Add(new AuditChange("name", before?.Name, after?.Name));
        }

        IReadOnlyList<string> previousPermissions = before?.Permissions ?? [];
        IReadOnlyList<string> currentPermissions = after?.Permissions ?? [];
        string[] added = currentPermissions.Except(previousPermissions, StringComparer.Ordinal).ToArray();
        string[] removed = previousPermissions.Except(currentPermissions, StringComparer.Ordinal).ToArray();

        if (before is null || after is null || added.Length > 0 || removed.Length > 0)
        {
            changes.Add(new AuditChange("permissions", Added: added, Removed: removed));
        }

        return changes;
    }
}
