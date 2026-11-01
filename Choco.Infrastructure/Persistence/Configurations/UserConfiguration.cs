using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.IdentityId)
            .HasMaxLength(User.IdentityIdMaxLength);

        builder.Property(user => user.Username)
            .HasMaxLength(User.UsernameMaxLength);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.DisplayNameMaxLength);

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(User.AvatarUrlMaxLength);

        builder.HasIndex(user => user.IdentityId)
            .IsUnique();

        builder.HasIndex(user => user.Username)
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
