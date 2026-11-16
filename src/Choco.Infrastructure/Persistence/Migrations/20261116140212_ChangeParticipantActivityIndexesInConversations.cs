using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeParticipantActivityIndexesInConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_second_user_id",
                table: "conversations");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_first_user_id_last_activity_at_id",
                table: "conversations",
                columns: new[] { "first_user_id", "last_activity_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_second_user_id_last_activity_at_id",
                table: "conversations",
                columns: new[] { "second_user_id", "last_activity_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_first_user_id_last_activity_at_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_second_user_id_last_activity_at_id",
                table: "conversations");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_second_user_id",
                table: "conversations",
                column: "second_user_id");
        }
    }
}
