using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillHandleReportsPermissionInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'reports.handle')
                WHERE system_key = 'admin' AND NOT ('reports.handle' = ANY (permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(permissions, 'reports.handle')
                WHERE system_key = 'admin';
                """);
        }
    }
}
