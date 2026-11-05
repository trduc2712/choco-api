using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public const string UserIdColumn = "UserId";

    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", table => table.HasComment(
            "Roles assigned to users, besides the default role that every signed-in user has."));

        builder.HasKey(userRole => userRole.Id);

        builder.Property(userRole => userRole.Id)
            .ValueGeneratedNever();

        builder.Property<Guid>(UserIdColumn)
            .HasComment("User who holds the role.");

        builder.Property(userRole => userRole.RoleId)
            .HasComment("Role the user holds.");

        builder.Property(userRole => userRole.AssignedAt)
            .HasComment("When the role was assigned.");

        builder.Property(userRole => userRole.AssignedBy)
            .HasComment("User who assigned the role; null when the system assigned it, such as the first administrator.");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userRole => userRole.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(UserIdColumn, nameof(UserRole.RoleId))
            .IsUnique();
    }
}
