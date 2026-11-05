using Choco.Domain.Roles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Choco.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", table => table.HasComment(
            "Named sets of permissions that users can hold. Roles and their permissions can change while the system runs."));

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.Name)
            .HasMaxLength(Role.NameMaxLength)
            .HasComment("Name shown to people; can be changed.");

        builder.Property(role => role.SystemKey)
            .HasMaxLength(Role.SystemKeyMaxLength)
            .HasComment("Fixed key of a role the system creates itself, such as admin; null for roles created by people.");

        builder.Property(role => role.IsDefault)
            .HasComment("True for the role every signed-in user has without being assigned it. At most one role is the default.");

        builder.PrimitiveCollection(role => role.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType(element => element.HasMaxLength(Permission.CodeMaxLength))
            .HasComment("Codes of the permissions the role grants, from the permission catalog in code.");

        builder.HasIndex(role => role.SystemKey)
            .IsUnique();

        builder.HasIndex(role => role.IsDefault)
            .IsUnique()
            .HasFilter("is_default");
    }
}
