using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Reports;

public sealed class Report : AggregateRoot
{
    public const int DetailsMaxLength = 500;
    public const int EnumMaxLength = 32;

    public Guid ReporterId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid? TargetPostId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ReportStatus Status { get; private set; }
    public ReportOutcome? Outcome { get; private set; }
    public Guid? ResolvedById { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private Report() { }

    private Report(
        Guid id,
        Guid reporterId,
        ReportTargetType targetType,
        Guid? targetPostId,
        Guid? targetUserId,
        ReportReason reason,
        string? details,
        DateTimeOffset createdAt)
        : base(id)
    {
        ReporterId = reporterId;
        TargetType = targetType;
        TargetPostId = targetPostId;
        TargetUserId = targetUserId;
        Reason = reason;
        Details = details;
        CreatedAt = createdAt;
        Status = ReportStatus.Open;
    }

    public static Result<Report> CreateForPost(
        Guid reporterId, Guid postId, Guid postAuthorId, ReportReason reason, string? details, DateTimeOffset now)
    {
        if (reporterId == postAuthorId)
        {
            return Result<Report>.Failure(ReportErrors.SelfReport);
        }

        return Result<Report>.Success(new Report(
            Guid.NewGuid(), reporterId, ReportTargetType.Post, postId, null, reason, NormalizeDetails(details), now));
    }

    public static Result<Report> CreateForUser(
        Guid reporterId, Guid userId, ReportReason reason, string? details, DateTimeOffset now)
    {
        if (reporterId == userId)
        {
            return Result<Report>.Failure(ReportErrors.SelfReport);
        }

        return Result<Report>.Success(new Report(
            Guid.NewGuid(), reporterId, ReportTargetType.User, null, userId, reason, NormalizeDetails(details), now));
    }

    public void Resolve(ReportOutcome outcome, Guid resolverId, DateTimeOffset now)
    {
        if (Status == ReportStatus.Resolved)
        {
            return;
        }

        Status = ReportStatus.Resolved;
        Outcome = outcome;
        ResolvedById = resolverId;
        ResolvedAt = now;
    }

    private static string? NormalizeDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        string trimmed = details.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, DetailsMaxLength, nameof(details));

        return trimmed;
    }
}
