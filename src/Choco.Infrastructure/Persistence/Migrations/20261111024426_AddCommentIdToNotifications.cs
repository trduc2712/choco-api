using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentIdToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "comment_id",
                table: "notifications",
                type: "uuid",
                nullable: true,
                comment: "Comment the notification is about; null when it is not about a comment.");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_comment_id",
                table: "notifications",
                column: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_comments_comment_id",
                table: "notifications",
                column: "comment_id",
                principalTable: "comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_comments_comment_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_comment_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "comment_id",
                table: "notifications");
        }
    }
}
