namespace Choco.Domain.Roles;

public static class SystemRoles
{
    public const string Member = "member";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<SystemRoleDefinition> All =
    [
        new(Member, "Member", IsDefault: true, Permissions: []),
        new(Admin, "Administrator", IsDefault: false, Permissions: [Permission.AssignRoles, Permission.ReadRoles, Permission.CreateRoles, Permission.UpdateRoles, Permission.DeleteRoles, Permission.DeleteAnyPost, Permission.DeleteAnyComment, Permission.HandleReports, Permission.ReadAnyFeedback, Permission.ChangeFeedbackStatus, Permission.ReadAuditLog, Permission.ReadAnyUser, Permission.ReadAnyContent, Permission.SuspendUsers])
    ];
}

public sealed record SystemRoleDefinition(
    string Key,
    string Name,
    bool IsDefault,
    IReadOnlyCollection<string> Permissions);
