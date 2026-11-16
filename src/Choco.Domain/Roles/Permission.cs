namespace Choco.Domain.Roles;

public static class Permission
{
    public const int CodeMaxLength = 64;

    public const string AssignRoles = "roles.assign";
    public const string ReadRoles = "roles.read";
    public const string CreateRoles = "roles.create";
    public const string UpdateRoles = "roles.update";
    public const string DeleteRoles = "roles.delete";
    public const string DeleteAnyPost = "posts.delete-any";
    public const string DeleteAnyComment = "comments.delete-any";
    public const string HandleReports = "reports.handle";
    public const string ReadAnyFeedback = "feedback.read-any";
    public const string ChangeFeedbackStatus = "feedback.change-status";
    public const string ReadAuditLog = "audit-log.read";
    public const string ReadAnyUser = "users.read-any";
    public const string ReadAnyContent = "content.read-any";
    public const string SuspendUsers = "users.suspend";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AssignRoles,
        ReadRoles,
        CreateRoles,
        UpdateRoles,
        DeleteRoles,
        DeleteAnyPost,
        DeleteAnyComment,
        HandleReports,
        ReadAnyFeedback,
        ChangeFeedbackStatus,
        ReadAuditLog,
        ReadAnyUser,
        ReadAnyContent,
        SuspendUsers
    };

    public static readonly IReadOnlySet<string> Sensitive = new HashSet<string>(StringComparer.Ordinal)
    {
        AssignRoles,
        CreateRoles,
        UpdateRoles,
        DeleteRoles,
        DeleteAnyPost,
        DeleteAnyComment,
        SuspendUsers
    };

    public static string Group(string code) => code switch
    {
        AssignRoles or ReadRoles or CreateRoles or UpdateRoles or DeleteRoles => "roles",
        DeleteAnyPost or DeleteAnyComment or ReadAnyContent => "content",
        HandleReports => "reports",
        ReadAnyFeedback or ChangeFeedbackStatus => "feedback",
        ReadAnyUser or SuspendUsers => "users",
        ReadAuditLog => "audit-log",
        _ => throw new ArgumentException("Unknown permission code.", nameof(code))
    };
}
