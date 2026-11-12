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
    UserSuspended,
    UserUnsuspended,
    RoleCreated,
    RoleUpdated,
    RoleDeleted,
    RoleAssigned,
    RoleRevoked,
    FeedbackStatusChanged
}
