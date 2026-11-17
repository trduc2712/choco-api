using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyDetailsToComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "depth",
                table: "comments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "How many replies deep the comment is: 0 for a top-level comment, 1 for a reply to it, and so on.");

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "comments",
                type: "uuid",
                nullable: true,
                comment: "Comment this one replies to; null for a comment written on the post itself.");

            migrationBuilder.AddColumn<Guid>(
                name: "root_id",
                table: "comments",
                type: "uuid",
                nullable: true,
                comment: "Top-level comment of the thread this reply belongs to; null for a top-level comment.");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_id_created_at_id",
                table: "comments",
                columns: new[] { "parent_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_root_id",
                table: "comments",
                column: "root_id");

            migrationBuilder.AddForeignKey(
                name: "fk_comments_comments_parent_id",
                table: "comments",
                column: "parent_id",
                principalTable: "comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_comments_comments_root_id",
                table: "comments",
                column: "root_id",
                principalTable: "comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_comments_comments_parent_id",
                table: "comments");

            migrationBuilder.DropForeignKey(
                name: "fk_comments_comments_root_id",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "ix_comments_parent_id_created_at_id",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "ix_comments_root_id",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "depth",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "root_id",
                table: "comments");
        }
    }
}
