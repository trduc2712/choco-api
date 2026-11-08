using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("user_follows", table => table.HasComment("Active follow relationships between users."));
        builder.HasKey(follow => follow.Id);
        builder.Property(follow => follow.Id).ValueGeneratedNever();
        builder.Property(follow => follow.FollowerId).HasComment("User who initiated the follow.");
        builder.Property(follow => follow.FollowedUserId).HasComment("User being followed.");
        builder.Property(follow => follow.FollowedAt).HasComment("When the user started following the other user.");

        builder.HasIndex(follow => new { follow.FollowerId, follow.FollowedUserId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(follow => follow.FollowedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
