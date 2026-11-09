using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDeleteAnyCommentPermissionInRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_append(permissions, 'comments.delete-any')
                WHERE system_key = 'admin' AND NOT ('comments.delete-any' = ANY (permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE roles
                SET permissions = array_remove(permissions, 'comments.delete-any')
                WHERE system_key = 'admin';
                """);
        }
    }
}
