namespace Choco.Domain.Roles;

public static class Permission
{
    public const int CodeMaxLength = 64;

    public const string AssignRoles = "roles.assign";
    public const string DeleteAnyPost = "posts.delete-any";
    public const string DeleteAnyComment = "comments.delete-any";
    public const string HandleReports = "reports.handle";
    public const string ReadAnyFeedback = "feedback.read-any";
    public const string ChangeFeedbackStatus = "feedback.change-status";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AssignRoles,
        DeleteAnyPost,
        DeleteAnyComment,
        HandleReports,
        ReadAnyFeedback,
        ChangeFeedbackStatus
    };
}
