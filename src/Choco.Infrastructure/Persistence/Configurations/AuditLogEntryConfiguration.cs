using Choco.Domain.AuditLogs;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs", table => table.HasComment(
            "Append-only record of who did what to which object. One row per successful action of a user or the system; rows are never changed."));

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.OccurredAt)
            .HasComment("When the action happened, taken from the API clock.");

        builder.Property(entry => entry.ActorType)
            .HasConversion<string>()
            .HasMaxLength(AuditLogEntry.EnumMaxLength)
            .HasComment("Who performed the action: User, or System for work no user triggered.");

        builder.Property(entry => entry.ActorId)
            .HasComment("User who performed the action; null when the actor is the system. Kept after the user deletes their account.");

        builder.Property(entry => entry.ActorRoles)
            .HasComment("Names of the roles the actor held when the action happened.");

        builder.Property(entry => entry.ClientId)
            .HasMaxLength(AuditLogEntry.ClientIdMaxLength)
            .HasComment("Client application that sent the request, taken from the authorized party of the access token.");

        builder.Property(entry => entry.Action)
            .HasConversion<string>()
            .HasMaxLength(AuditLogEntry.EnumMaxLength)
            .HasComment("What was done, for example PostDeleted or RoleAssigned.");

        builder.Property(entry => entry.TargetType)
            .HasConversion<string>()
            .HasMaxLength(AuditLogEntry.EnumMaxLength)
            .HasComment("Kind of object the action was done to: Post, Comment, User, Report, Feedback, Role, Request or AuditLog.");

        builder.Property(entry => entry.TargetId)
            .HasComment("Id of the object the action was done to; the table it points to depends on target_type.");

        builder.Property(entry => entry.AffectedUserId)
            .HasComment("User whose data or account the action changed, when it is not the actor, for example the author of a removed post.");

        builder.Property(entry => entry.Detail)
            .HasMaxLength(AuditLogEntry.DetailMaxLength)
            .HasComment("Short note that qualifies the action, such as a role name or a new status. Never holds post or comment content.");

        builder.Property(entry => entry.TraceId)
            .HasMaxLength(AuditLogEntry.TraceIdMaxLength)
            .HasComment("Trace id of the request that caused the action, to find its application log lines.");

        builder.Property(entry => entry.UserAgent)
            .HasMaxLength(AuditLogEntry.UserAgentMaxLength)
            .HasComment("User agent the client sent with the request, cut to the column length.");

        builder.Property(entry => entry.IpAddress)
            .HasComment("Address of the client that sent the request, taken from the forwarded headers of the trusted proxy.");

        builder.HasIndex(entry => new { entry.OccurredAt, entry.Id })
            .IsDescending(true, true);

        builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAt });

        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId, entry.OccurredAt });

        builder.HasIndex(entry => new { entry.AffectedUserId, entry.OccurredAt });
    }
}
