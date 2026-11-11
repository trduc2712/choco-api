using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback_entries", table => table.HasComment(
            "Bug reports and suggestions about the app that signed-in users send, waiting for or finished with an administrator's triage."));

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.AuthorId)
            .HasComment("User who sent the feedback.");

        builder.Property(entry => entry.Category)
            .HasConversion<string>()
            .HasMaxLength(FeedbackEntry.EnumMaxLength)
            .HasComment("What kind of feedback it is: Bug, Suggestion or Other.");

        builder.Property(entry => entry.Message)
            .HasMaxLength(FeedbackEntry.MessageMaxLength)
            .HasComment("What the user wrote, trimmed.");

        builder.Property(entry => entry.Status)
            .HasConversion<string>()
            .HasMaxLength(FeedbackEntry.EnumMaxLength)
            .HasComment("Triage state: New, InProgress, Resolved or WontFix.");

        builder.ComplexProperty(entry => entry.Context, context =>
        {
            context.Property(value => value.Path)
                .HasColumnName("context_path")
                .HasMaxLength(FeedbackContext.PathMaxLength)
                .HasComment("Pathname of the page the user was on, without query string or hash.");

            context.Property(value => value.Locale)
                .HasColumnName("context_locale")
                .HasConversion<string>()
                .HasMaxLength(FeedbackEntry.EnumMaxLength)
                .HasComment("Language of the app when the feedback was sent: En, Ja or Vi.");

            context.Property(value => value.Theme)
                .HasColumnName("context_theme")
                .HasConversion<string>()
                .HasMaxLength(FeedbackEntry.EnumMaxLength)
                .HasComment("Theme of the app when the feedback was sent: Light, Dark or System.");

            context.Property(value => value.UserAgent)
                .HasColumnName("context_user_agent")
                .HasMaxLength(FeedbackContext.UserAgentMaxLength)
                .HasComment("Browser user agent reported by the web app.");

            context.Property(value => value.ViewportWidth)
                .HasColumnName("context_viewport_width")
                .HasComment("Width of the browser viewport in pixels.");

            context.Property(value => value.ViewportHeight)
                .HasColumnName("context_viewport_height")
                .HasComment("Height of the browser viewport in pixels.");

            context.Property(value => value.AppVersion)
                .HasColumnName("context_app_version")
                .HasMaxLength(FeedbackContext.AppVersionMaxLength)
                .HasComment("Build commit SHA of the web app.");
        });

        builder.HasIndex(entry => new { entry.CreatedAt, entry.Id })
            .IsDescending(true, true)
            .HasDatabaseName("ix_feedback_entries_created_at_id");

        builder.HasIndex(entry => entry.AuthorId)
            .HasDatabaseName("ix_feedback_entries_author_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
