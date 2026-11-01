using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasComment(
            "People who use choco-api. Sign-in lives in Clerk; each row is the local copy of one Clerk user."));

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.IdentityId)
            .HasMaxLength(User.IdentityIdMaxLength)
            .HasComment("Clerk user id (the sub claim) that links the row to its Clerk account.");

        builder.Property(user => user.Username)
            .HasMaxLength(User.UsernameMaxLength)
            .HasComment("Handle copied from Clerk. Unique among users that are not deleted.");

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.DisplayNameMaxLength)
            .HasComment("Name shown to other users. Defaults to the username when Clerk has no name.");

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(User.AvatarUrlMaxLength)
            .HasComment("Profile image URL from Clerk; null when the user has none.");

        builder.HasIndex(user => user.IdentityId)
            .IsUnique();

        builder.HasIndex(user => user.Username)
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
