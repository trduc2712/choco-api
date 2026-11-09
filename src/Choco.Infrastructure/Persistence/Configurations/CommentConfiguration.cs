using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments", table => table.HasComment("Comments that users write on posts."));

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .ValueGeneratedNever();

        builder.Property(comment => comment.PostId)
            .HasComment("Post the comment belongs to.");

        builder.Property(comment => comment.AuthorId)
            .HasComment("User who wrote the comment.");

        builder.Property(comment => comment.Content)
            .HasMaxLength(Comment.ContentMaxLength)
            .HasComment("Comment text as the author wrote it.");

        builder.HasIndex(comment => new { comment.PostId, comment.CreatedAt, comment.Id });

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
