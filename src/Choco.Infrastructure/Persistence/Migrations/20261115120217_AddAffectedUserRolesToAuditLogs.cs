using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffectedUserRolesToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "affected_user_roles",
                table: "audit_logs",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]",
                comment: "Names of the roles the affected user held right after the action; empty when there is no affected user or the row is older than this column.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "affected_user_roles",
                table: "audit_logs");
        }
    }
}
