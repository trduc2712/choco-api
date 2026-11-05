using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts", table => table.HasComment(
            "Text posts that users publish."));

        builder.HasKey(post => post.Id);

        builder.Property(post => post.Id)
            .ValueGeneratedNever();

        builder.Property(post => post.AuthorId)
            .HasComment("User who wrote the post.");

        builder.Property(post => post.Content)
            .HasMaxLength(Post.ContentMaxLength)
            .HasComment("Post text as the author wrote it.");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
