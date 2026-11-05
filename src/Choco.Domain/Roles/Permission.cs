namespace Choco.Domain.Roles;

public static class Permission
{
    public const int CodeMaxLength = 64;

    public const string AssignRoles = "roles.assign";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AssignRoles
    };
}
