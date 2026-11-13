using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangesToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "changes",
                table: "audit_logs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "Fields changed by the action. Content and personal data are represented by field name only.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "changes",
                table: "audit_logs");
        }
    }
}
