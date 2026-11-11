using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports", table =>
        {
            table.HasComment("Reports from users about a post or another user, waiting for or finished with a moderator's decision.");
            table.HasCheckConstraint(
                "ck_reports_target",
                "(target_post_id IS NOT NULL AND target_user_id IS NULL AND target_type = 'Post') "
                + "OR (target_post_id IS NULL AND target_user_id IS NOT NULL AND target_type = 'User')");
        });
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.ReporterId).HasComment("User who made the report.");
        builder.Property(report => report.TargetType)
            .HasConversion<string>()
            .HasMaxLength(Report.EnumMaxLength)
            .HasComment("What was reported, Post or User.");
        builder.Property(report => report.TargetPostId).HasComment("Reported post; null unless the target is a post.");
        builder.Property(report => report.TargetUserId).HasComment("Reported user; null unless the target is a user.");
        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(Report.EnumMaxLength)
            .HasComment("Why it was reported, such as Spam or Harassment.");
        builder.Property(report => report.Details)
            .HasMaxLength(Report.DetailsMaxLength)
            .HasComment("Explanation written by the reporter; null when none was given.");
        builder.Property(report => report.CreatedAt).HasComment("When the report was made.");
        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(Report.EnumMaxLength)
            .HasComment("Open while waiting for a decision, Resolved afterwards.");
        builder.Property(report => report.Outcome)
            .HasConversion<string>()
            .HasMaxLength(Report.EnumMaxLength)
            .HasComment("Decision on the report, Dismissed or ActionTaken; null while open.");
        builder.Property(report => report.ResolvedById).HasComment("Moderator who decided; null while open.");
        builder.Property(report => report.ResolvedAt).HasComment("When the decision was made; null while open.");

        builder.HasIndex(report => new { report.ReporterId, report.TargetPostId })
            .IsUnique()
            .HasFilter("status = 'Open' AND target_post_id IS NOT NULL")
            .HasDatabaseName("ix_reports_reporter_id_target_post_id_open");

        builder.HasIndex(report => new { report.ReporterId, report.TargetUserId })
            .IsUnique()
            .HasFilter("status = 'Open' AND target_user_id IS NOT NULL")
            .HasDatabaseName("ix_reports_reporter_id_target_user_id_open");

        builder.HasIndex(report => new { report.CreatedAt, report.Id })
            .HasFilter("status = 'Open'")
            .HasDatabaseName("ix_reports_created_at_id_open");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(report => report.TargetPostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ResolvedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
