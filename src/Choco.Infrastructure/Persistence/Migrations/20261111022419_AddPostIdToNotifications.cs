using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostIdToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "notifications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "What happened, such as Followed, PostLiked or PostCommented.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "What happened, such as Followed.");

            migrationBuilder.AddColumn<Guid>(
                name: "post_id",
                table: "notifications",
                type: "uuid",
                nullable: true,
                comment: "Post the notification is about; null when it is not about a post.");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_post_id",
                table: "notifications",
                column: "post_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_post_post_id",
                table: "notifications",
                column: "post_id",
                principalTable: "posts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_post_post_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_post_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "post_id",
                table: "notifications");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "notifications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "What happened, such as Followed.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "What happened, such as Followed, PostLiked or PostCommented.");
        }
    }
}
