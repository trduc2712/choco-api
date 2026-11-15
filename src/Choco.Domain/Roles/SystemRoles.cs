namespace Choco.Domain.Roles;

public static class SystemRoles
{
    public const string Member = "member";
    public const string Admin = "admin";
    public const string Moderator = "moderator";

    public static readonly IReadOnlyList<SystemRoleDefinition> All =
    [
        new(Member, "Member", IsDefault: true, Permissions: [],
            "Provides the standard role for signed-in users.", RoleColors.Gray, RoleIcons.User),
        new(Admin, "Administrator", IsDefault: false, Permissions: [Permission.AssignRoles, Permission.ReadRoles, Permission.CreateRoles, Permission.UpdateRoles, Permission.DeleteRoles, Permission.DeleteAnyPost, Permission.DeleteAnyComment, Permission.HandleReports, Permission.ReadAnyFeedback, Permission.ChangeFeedbackStatus, Permission.ReadAuditLog, Permission.ReadAnyUser, Permission.ReadAnyContent, Permission.SuspendUsers],
            "Manages roles, users, and community operations.", RoleColors.Purple, RoleIcons.Crown),
        new(Moderator, "Moderator", IsDefault: false, Permissions: [Permission.HandleReports, Permission.DeleteAnyPost, Permission.DeleteAnyComment, Permission.ReadAnyContent, Permission.SuspendUsers, Permission.ReadAnyUser],
            "Reviews reports and moderates community content and users.", RoleColors.Teal, RoleIcons.Shield)
    ];
}

public sealed record SystemRoleDefinition(
    string Key,
    string Name,
    bool IsDefault,
    IReadOnlyCollection<string> Permissions,
    string? Description = null,
    string Color = RoleColors.Gray,
    string Icon = RoleIcons.Shield);
