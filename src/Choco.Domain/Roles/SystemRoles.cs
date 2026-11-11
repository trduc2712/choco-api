namespace Choco.Domain.Roles;

public static class SystemRoles
{
    public const string Member = "member";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<SystemRoleDefinition> All =
    [
        new(Member, "Member", IsDefault: true, Permissions: []),
        new(Admin, "Administrator", IsDefault: false, Permissions: [Permission.AssignRoles, Permission.DeleteAnyPost, Permission.DeleteAnyComment, Permission.HandleReports])
    ];
}

public sealed record SystemRoleDefinition(
    string Key,
    string Name,
    bool IsDefault,
    IReadOnlyCollection<string> Permissions);
