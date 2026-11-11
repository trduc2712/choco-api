using System.Net;

using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.AuditLogs;

public sealed class AuditLogEntry : BaseEntity
{
    public const int EnumMaxLength = 32;
    public const int ClientIdMaxLength = 256;
    public const int DetailMaxLength = 200;
    public const int TraceIdMaxLength = 32;
    public const int UserAgentMaxLength = 512;

    public DateTimeOffset OccurredAt { get; private set; }
    public AuditActorType ActorType { get; private set; }
    public Guid? ActorId { get; private set; }
    public string[] ActorRoles { get; private set; } = [];
    public string? ClientId { get; private set; }
    public AuditAction Action { get; private set; }
    public AuditTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid? AffectedUserId { get; private set; }
    public string? Detail { get; private set; }
    public string? TraceId { get; private set; }
    public string? UserAgent { get; private set; }
    public IPAddress? IpAddress { get; private set; }

    private AuditLogEntry() { }

    private AuditLogEntry(
        Guid id,
        DateTimeOffset occurredAt,
        AuditActorType actorType,
        Guid? actorId,
        string[] actorRoles,
        string? clientId,
        AuditAction action,
        AuditTargetType targetType,
        Guid targetId,
        Guid? affectedUserId,
        string? detail,
        string? traceId,
        string? userAgent,
        IPAddress? ipAddress)
        : base(id)
    {
        OccurredAt = occurredAt;
        ActorType = actorType;
        ActorId = actorId;
        ActorRoles = actorRoles;
        ClientId = clientId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        AffectedUserId = affectedUserId;
        Detail = detail;
        TraceId = traceId;
        UserAgent = userAgent;
        IpAddress = ipAddress;
    }

    public static AuditLogEntry Create(
        DateTimeOffset occurredAt,
        AuditActorType actorType,
        Guid? actorId,
        IEnumerable<string> actorRoles,
        string? clientId,
        AuditAction action,
        AuditTargetType targetType,
        Guid targetId,
        Guid? affectedUserId,
        string? detail,
        string? traceId,
        string? userAgent,
        IPAddress? ipAddress)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);

        if (actorType == AuditActorType.User && (actorId is null || actorId == Guid.Empty))
        {
            throw new ArgumentException("A user actor needs an id.", nameof(actorId));
        }

        if (actorType == AuditActorType.System && actorId is not null)
        {
            throw new ArgumentException("A system actor has no id.", nameof(actorId));
        }

        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(targetId));
        }

        if (detail is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(detail.Length, DetailMaxLength, nameof(detail));
        }

        if (traceId is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(traceId.Length, TraceIdMaxLength, nameof(traceId));
        }

        return new AuditLogEntry(
            Guid.NewGuid(),
            occurredAt,
            actorType,
            actorId,
            [.. actorRoles],
            Truncate(clientId, ClientIdMaxLength),
            action,
            targetType,
            targetId,
            affectedUserId,
            detail,
            traceId,
            Truncate(userAgent, UserAgentMaxLength),
            ipAddress);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
