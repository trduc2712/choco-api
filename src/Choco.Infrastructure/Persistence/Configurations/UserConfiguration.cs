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

        builder.Property(user => user.Bio)
            .HasMaxLength(User.BioMaxLength)
            .HasComment("Short text the user wrote about themselves. Lives only in choco-api, not in Clerk; null when the user has none.");

        builder.Property(user => user.IdentityUpdatedAt)
            .HasComment("When Clerk last changed the account, taken from the event or the Clerk response that last wrote the profile; an event with an earlier or equal time is ignored. Null until the first sync.");

        builder.Property(user => user.SuspendedAt)
            .HasComment("When an administrator suspended the user; null when the user is not suspended. A suspended user can read but cannot create or change anything.");

        builder.Property(user => user.SuspendedBy)
            .HasComment("Administrator who suspended the user; null when the user is not suspended.");

        builder.Property(user => user.SuspensionReason)
            .HasMaxLength(User.SuspensionReasonMaxLength)
            .HasComment("Why the user was suspended, shown to the user; null when the user is not suspended.");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(user => user.SuspendedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(user => user.IdentityId)
            .IsUnique();

        builder.HasIndex(user => user.Username)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasMany(user => user.Roles)
            .WithOne()
            .HasForeignKey(UserRoleConfiguration.UserIdColumn)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
