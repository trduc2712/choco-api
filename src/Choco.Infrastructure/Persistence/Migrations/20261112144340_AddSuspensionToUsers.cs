using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspensionToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When an administrator suspended the user; null when the user is not suspended. A suspended user can read but cannot create or change anything.");

            migrationBuilder.AddColumn<Guid>(
                name: "suspended_by",
                table: "users",
                type: "uuid",
                nullable: true,
                comment: "Administrator who suspended the user; null when the user is not suspended.");

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                table: "users",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                comment: "Why the user was suspended, shown to the user; null when the user is not suspended.");

            migrationBuilder.CreateIndex(
                name: "ix_users_suspended_by",
                table: "users",
                column: "suspended_by");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_suspended_by",
                table: "users",
                column: "suspended_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_suspended_by",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_suspended_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspended_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                table: "users");
        }
    }
}
