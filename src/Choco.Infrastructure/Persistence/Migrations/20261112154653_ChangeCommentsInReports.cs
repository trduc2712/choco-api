using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCommentsInReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "reports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                comment: "Decision on the report: Dismissed, ContentRemoved or UserSuspended; null while open.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "Decision on the report, Dismissed or ActionTaken; null while open.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "reports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                comment: "Decision on the report, Dismissed or ActionTaken; null while open.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "Decision on the report: Dismissed, ContentRemoved or UserSuspended; null while open.");
        }
    }
}
