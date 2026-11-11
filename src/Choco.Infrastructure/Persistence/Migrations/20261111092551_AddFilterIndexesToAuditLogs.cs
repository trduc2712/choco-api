using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterIndexesToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_affected_user_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "affected_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at_id",
                table: "audit_logs",
                columns: new[] { "occurred_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target_type_target_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "target_type", "target_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_actor_id_occurred_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_affected_user_id_occurred_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_occurred_at_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_target_type_target_id_occurred_at",
                table: "audit_logs");
        }
    }
}
