namespace Choco.Domain.Roles;

public static class Permission
{
    public const int CodeMaxLength = 64;

    public const string AssignRoles = "roles.assign";
    public const string DeleteAnyPost = "posts.delete-any";
    public const string DeleteAnyComment = "comments.delete-any";
    public const string HandleReports = "reports.handle";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AssignRoles,
        DeleteAnyPost,
        DeleteAnyComment,
        HandleReports
    };
}
