using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCommentsInAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "target_type",
                table: "audit_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "Kind of object the action was done to: Post, Comment, User, Report, Feedback or Role.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "Kind of object the action was done to: Post, Comment, User, Report or Feedback.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "target_type",
                table: "audit_logs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "Kind of object the action was done to: Post, Comment, User, Report or Feedback.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "Kind of object the action was done to: Post, Comment, User, Report, Feedback or Role.");
        }
    }
}
