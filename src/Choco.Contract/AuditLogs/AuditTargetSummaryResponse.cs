using System.Text.Json.Serialization;

namespace Choco.Contract.AuditLogs;

/// <summary>
/// The current state of an entry's target, read when the entry is requested, not when the action happened.
/// Which properties are present depends on the target type; a property that does not apply or is not known is left out.
/// </summary>
/// <param name="Excerpt">Feedback, Post, Comment, Report: the first 160 characters of the message, content or report details. Only for a caller who may read that kind of object.</param>
/// <param name="Author">Feedback, Post, Comment: the user who wrote it.</param>
/// <param name="Category">Feedback: its category, such as bug.</param>
/// <param name="Status">Feedback: its status, such as in_progress. Report: Open or Resolved.</param>
/// <param name="IsDeleted">Post, Comment, User: whether it is deleted.</param>
/// <param name="PostId">Comment: the post it belongs to.</param>
/// <param name="Reason">Report: why it was reported, such as Spam.</param>
/// <param name="Outcome">Report: how it was resolved; left out while it is open.</param>
/// <param name="ReportedType">Report: the kind of object reported: Post or User.</param>
/// <param name="ReportedId">Report: the id of the object reported.</param>
/// <param name="Reporter">Report: the user who reported it.</param>
/// <param name="Username">User: the username.</param>
/// <param name="DisplayName">User: the display name. Only for a caller who may read any user.</param>
/// <param name="AvatarUrl">User: the avatar URL, when there is one. Only for a caller who may read any user.</param>
/// <param name="IsSuspended">User: whether the account is suspended.</param>
/// <param name="Name">Role: the role name.</param>
/// <param name="PermissionCount">Role: the number of permissions it grants.</param>
/// <param name="HolderCount">Role: the number of users who hold it.</param>
/// <example>{"excerpt":"Feedback message","author":{"id":"00000000-0000-0000-0000-000000000001","username":"username"},"category":"bug","status":"in_progress"}</example>
public sealed record AuditTargetSummaryResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Excerpt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuditLogUserResponse? Author = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Category = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsDeleted = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? PostId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReportedType = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ReportedId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuditLogUserResponse? Reporter = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Username = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AvatarUrl = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsSuspended = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PermissionCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HolderCount = null);
