using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReadAnyContentPermissionInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'content.read-any')
                WHERE system_key = 'admin' AND NOT ('content.read-any' = ANY (permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(permissions, 'content.read-any')
                WHERE system_key = 'admin';
                """);
        }
    }
}
