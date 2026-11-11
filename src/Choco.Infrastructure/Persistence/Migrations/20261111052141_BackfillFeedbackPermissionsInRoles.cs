using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillFeedbackPermissionsInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'feedback.read-any')
                WHERE system_key = 'admin' AND NOT ('feedback.read-any' = ANY (permissions));
                """);

            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'feedback.change-status')
                WHERE system_key = 'admin' AND NOT ('feedback.change-status' = ANY (permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(array_remove(permissions, 'feedback.read-any'), 'feedback.change-status')
                WHERE system_key = 'admin';
                """);
        }
    }
}
