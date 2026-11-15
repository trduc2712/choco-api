using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Feedback;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLog;

internal sealed class GetAuditLogQueryHandler(
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IFeedbackEntryRepository feedbackRepository,
    IReportRepository reportRepository,
    IRoleRepository roleRepository,
    IPermissionChecker permissionChecker) : IQueryHandler<GetAuditLogQuery, AuditLogResponse>
{
    private const int NewerPreferred = 2;

    public async Task<Result<AuditLogResponse>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        AuditLogEntry? entry = await auditLogRepository.GetByIdForReadAsync(query.Id, cancellationToken);
        if (entry is null)
        {
            return Result<AuditLogResponse>.Failure(AuditLogErrors.NotFound);
        }

        Result<bool> canReadDetails = await CanReadDetailsAsync(entry.TargetType, cancellationToken);
        if (canReadDetails.IsFailure)
        {
            return Result<AuditLogResponse>.Failure(canReadDetails.Error);
        }

        Target target = await LoadTargetAsync(entry, cancellationToken);

        List<Guid> userIds = new[] { entry.ActorId, entry.AffectedUserId, target.UserId }
            .OfType<Guid>()
            .Distinct()
            .ToList();
        IReadOnlyList<User> users = userIds.Count == 0
            ? []
            : await userRepository.ListByIdsIncludingDeletedForReadAsync(userIds, cancellationToken);
        Dictionary<Guid, string> usernames = users.ToDictionary(user => user.Id, user => user.Username);
        User? targetUser = entry.TargetType == AuditTargetType.User
            ? users.SingleOrDefault(user => user.Id == entry.TargetId)
            : null;

        AuditTargetSummaryResponse? summary = Summarize(target, targetUser, usernames, canReadDetails.Value);
        AuditTargetHistoryResponse history = await GetHistoryAsync(entry, cancellationToken);
        IReadOnlyDictionary<string, DateTimeOffset> previousChanges = await auditLogRepository.GetLastChangesBeforeForReadAsync(
            entry, entry.Changes.Select(change => change.Field).Distinct().ToArray(), cancellationToken);

        return Result<AuditLogResponse>.Success(entry.ToResponse(usernames, previousChanges, summary, history));
    }

    private async Task<Result<bool>> CanReadDetailsAsync(AuditTargetType targetType, CancellationToken cancellationToken)
    {
        string? permission = targetType switch
        {
            AuditTargetType.Post or AuditTargetType.Comment => Permission.ReadAnyContent,
            AuditTargetType.Feedback => Permission.ReadAnyFeedback,
            AuditTargetType.Report => Permission.HandleReports,
            AuditTargetType.User => Permission.ReadAnyUser,
            _ => null
        };

        return permission is null
            ? Result<bool>.Success(false)
            : await permissionChecker.IsGrantedToCurrentUserAsync(permission, PermissionScope.Platform, cancellationToken);
    }

    private async Task<Target> LoadTargetAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        switch (entry.TargetType)
        {
            case AuditTargetType.Post:
                Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(entry.TargetId, cancellationToken);
                return new Target(post, post?.AuthorId);
            case AuditTargetType.Comment:
                Comment? comment = await commentRepository.GetByIdIncludingDeletedForReadAsync(entry.TargetId, cancellationToken);
                return new Target(comment, comment?.AuthorId);
            case AuditTargetType.Feedback:
                FeedbackEntry? feedback = await feedbackRepository.GetByIdForReadAsync(entry.TargetId, cancellationToken);
                return new Target(feedback, feedback?.AuthorId);
            case AuditTargetType.Report:
                Report? report = await reportRepository.GetByIdForReadAsync(entry.TargetId, cancellationToken);
                return new Target(report, report?.ReporterId);
            case AuditTargetType.User:
                return new Target(null, entry.TargetId);
            case AuditTargetType.Role:
                Role? role = await roleRepository.GetByIdForReadAsync(entry.TargetId, cancellationToken);
                if (role is null)
                {
                    return new Target(null, null);
                }

                int holderCount = await userRepository.CountWithRoleAsync(role.Id, cancellationToken);
                return new Target(new RoleTarget(role, holderCount), null);
            default:
                return new Target(null, null);
        }
    }

    private static AuditTargetSummaryResponse? Summarize(
        Target target, User? targetUser, Dictionary<Guid, string> usernames, bool canReadDetails)
    {
        AuditLogUserResponse? user = AuditLogMappings.ToUser(target.UserId, usernames);

        if (targetUser is not null)
        {
            return new AuditTargetSummaryResponse(
                IsDeleted: targetUser.IsDeleted,
                Username: targetUser.Username,
                DisplayName: canReadDetails ? targetUser.DisplayName : null,
                AvatarUrl: canReadDetails ? targetUser.AvatarUrl : null,
                IsSuspended: targetUser.SuspendedAt is not null);
        }

        return target.Value switch
        {
            Post post => new AuditTargetSummaryResponse(
                Excerpt: canReadDetails ? Excerpt(post.Content) : null,
                Author: user,
                IsDeleted: post.IsDeleted),
            Comment comment => new AuditTargetSummaryResponse(
                Excerpt: canReadDetails ? Excerpt(comment.Content) : null,
                Author: user,
                IsDeleted: comment.IsDeleted,
                PostId: comment.PostId),
            FeedbackEntry feedback => new AuditTargetSummaryResponse(
                Excerpt: canReadDetails ? Excerpt(feedback.Message) : null,
                Author: user,
                Category: FeedbackWire.ToWire(feedback.Category),
                Status: FeedbackWire.ToWire(feedback.Status)),
            Report report => new AuditTargetSummaryResponse(
                Excerpt: canReadDetails && report.Details is { } details ? Excerpt(details) : null,
                Status: report.Status.ToString(),
                Reason: report.Reason.ToString(),
                Outcome: report.Outcome?.ToString(),
                ReportedType: report.TargetType.ToString(),
                ReportedId: report.TargetPostId ?? report.TargetUserId,
                Reporter: user),
            RoleTarget role => new AuditTargetSummaryResponse(
                Name: role.Role.Name,
                PermissionCount: role.Role.Permissions.Count,
                HolderCount: role.HolderCount),
            _ => null
        };
    }

    private async Task<AuditTargetHistoryResponse> GetHistoryAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        AuditLogFilter sameTarget = new(null, null, entry.TargetType, entry.TargetId, null, null, null);
        int totalCount = await auditLogRepository.CountForReadAsync(sameTarget, cancellationToken);

        int others = GetAuditLogQuery.RecentHistorySize - 1;
        IReadOnlyList<AuditLogEntry> around = totalCount <= 1
            ? []
            : await auditLogRepository.ListAroundForReadAsync(entry, others, others, cancellationToken);

        List<AuditLogEntry> older = around.Where(other => other.OccurredAt <= entry.OccurredAt).ToList();
        List<AuditLogEntry> newer = around.Where(other => other.OccurredAt > entry.OccurredAt).ToList();
        int newerCount = Math.Min(newer.Count, Math.Max(NewerPreferred, others - older.Count));
        int olderCount = Math.Min(older.Count, others - newerCount);

        IEnumerable<AuditLogEntry> recent = older.TakeLast(olderCount)
            .Append(entry)
            .Concat(newer.Take(newerCount));

        return new AuditTargetHistoryResponse(
            totalCount,
            recent.Select(other => new AuditTargetEventResponse(
                other.Id, other.Action.ToString(), other.OccurredAt, ValueOf(other))).ToArray());
    }

    private static string? ValueOf(AuditLogEntry entry)
    {
        AuditChange? status = entry.Changes.FirstOrDefault(change => change.Field == "status" && change.After is not null);

        return (status ?? entry.Changes.FirstOrDefault(change => change.After is not null))?.After;
    }

    private static string Excerpt(string text)
    {
        if (text.Length <= GetAuditLogQuery.ExcerptMaxLength)
        {
            return text;
        }

        int length = GetAuditLogQuery.ExcerptMaxLength;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length].TrimEnd() + "…";
    }

    private sealed record Target(object? Value, Guid? UserId);

    private sealed record RoleTarget(Role Role, int HolderCount);
}
