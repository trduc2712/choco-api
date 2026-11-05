namespace Choco.Domain.Roles;

public sealed record PermissionScope
{
    public static readonly PermissionScope Platform = new();

    private PermissionScope() { }
}
