namespace Choco.Domain.AuditLogs;

public enum AuditAction
{
    PostCreated,
    PostEdited,
    PostDeleted,
    CommentCreated,
    CommentDeleted,
    ProfileUpdated,
    BioUpdated,
    AvatarUploaded,
    AvatarRemoved,
    AccountDeleted,
    PostReported,
    UserReported,
    ReportResolved,
    UserBlocked,
    UserUnblocked,
    RoleCreated,
    RoleAssigned,
    RoleRevoked,
    FeedbackStatusChanged
}
