using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDeleteAnyPostPermissionInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'posts.delete-any')
                WHERE system_key = 'admin' AND NOT ('posts.delete-any' = ANY (permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(permissions, 'posts.delete-any')
                WHERE system_key = 'admin';
                """);
        }
    }
}
