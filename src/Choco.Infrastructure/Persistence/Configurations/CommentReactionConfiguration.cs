using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> builder)
    {
        builder.ToTable("comment_reactions", table => table.HasComment("Likes and dislikes that users give to comments, at most one per user and comment."));
        builder.HasKey(reaction => reaction.Id);
        builder.Property(reaction => reaction.Id).ValueGeneratedNever();
        builder.Property(reaction => reaction.CommentId).HasComment("Comment that was reacted to.");
        builder.Property(reaction => reaction.UserId).HasComment("User who reacted to the comment.");
        builder.Property(reaction => reaction.Type)
            .HasConversion<string>()
            .HasMaxLength(CommentReaction.TypeMaxLength)
            .HasComment("The reaction: Like or Dislike.");
        builder.Property(reaction => reaction.ReactedAt).HasComment("When the user gave the current reaction.");

        builder.HasIndex(reaction => new { reaction.CommentId, reaction.UserId }).IsUnique();

        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(reaction => reaction.CommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(reaction => reaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
