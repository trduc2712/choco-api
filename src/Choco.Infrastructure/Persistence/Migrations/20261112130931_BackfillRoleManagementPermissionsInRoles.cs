using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRoleManagementPermissionsInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = permissions || ARRAY(
                    SELECT permission
                    FROM unnest(ARRAY['roles.read', 'roles.create', 'roles.update', 'roles.delete']::character varying(64)[]) AS permission
                    WHERE NOT (permission = ANY (roles.permissions)))
                WHERE system_key = 'admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(array_remove(array_remove(array_remove(
                    permissions, 'roles.read'), 'roles.create'), 'roles.update'), 'roles.delete')
                WHERE system_key = 'admin';
                """);
        }
    }
}
