using Choco.Domain.Notifications;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", table => table.HasComment("Things that happened to a user, such as a new follower, shown to that user."));
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.RecipientId).HasComment("User who is notified.");
        builder.Property(notification => notification.ActorId).HasComment("User whose action caused the notification.");
        builder.Property(notification => notification.Type)
            .HasConversion<string>()
            .HasMaxLength(Notification.TypeMaxLength)
            .HasComment("What happened, such as Followed.");
        builder.Property(notification => notification.CreatedAt).HasComment("When it happened.");
        builder.Property(notification => notification.ReadAt).HasComment("When the recipient marked it read; null while unread.");

        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAt, notification.Id })
            .IsDescending(false, true, true);

        builder.HasIndex(notification => notification.RecipientId)
            .HasFilter("read_at IS NULL")
            .HasDatabaseName("ix_notifications_recipient_id_unread");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
