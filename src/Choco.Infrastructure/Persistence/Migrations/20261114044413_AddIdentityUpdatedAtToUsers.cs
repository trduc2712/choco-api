using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityUpdatedAtToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "identity_updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When Clerk last changed the account, taken from the event or the Clerk response that last wrote the profile; an event with an earlier or equal time is ignored. Null until the first sync.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_updated_at",
                table: "users");
        }
    }
}
