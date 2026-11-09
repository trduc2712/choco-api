using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("post_likes", table => table.HasComment("Active likes that users give to posts."));
        builder.HasKey(like => like.Id);
        builder.Property(like => like.Id).ValueGeneratedNever();
        builder.Property(like => like.PostId).HasComment("Post that was liked.");
        builder.Property(like => like.UserId).HasComment("User who liked the post.");
        builder.Property(like => like.LikedAt).HasComment("When the user liked the post.");

        builder.HasIndex(like => new { like.PostId, like.UserId }).IsUnique();

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(like => like.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
