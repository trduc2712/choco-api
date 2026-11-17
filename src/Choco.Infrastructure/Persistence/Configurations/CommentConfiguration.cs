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

        builder.Property(comment => comment.ParentId)
            .HasComment("Comment this one replies to; null for a comment written on the post itself.");

        builder.Property(comment => comment.RootId)
            .HasComment("Top-level comment of the thread this reply belongs to; null for a top-level comment.");

        builder.Property(comment => comment.Depth)
            .HasComment("How many replies deep the comment is: 0 for a top-level comment, 1 for a reply to it, and so on.");

        builder.HasIndex(comment => new { comment.PostId, comment.CreatedAt, comment.Id });

        builder.HasIndex(comment => new { comment.ParentId, comment.CreatedAt, comment.Id });

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(comment => comment.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(comment => comment.RootId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
