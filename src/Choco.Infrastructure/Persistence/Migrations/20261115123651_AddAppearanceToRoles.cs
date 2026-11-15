using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppearanceToRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "gray",
                comment: "Color key for displaying the role.");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Optional description of what the role is for.");

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "shield",
                comment: "Icon key for displaying the role.");

            migrationBuilder.Sql("""
                UPDATE roles
                SET color = CASE system_key
                        WHEN 'admin' THEN 'purple'
                        WHEN 'moderator' THEN 'teal'
                        ELSE 'gray'
                    END,
                    icon = CASE system_key
                        WHEN 'member' THEN 'user'
                        WHEN 'admin' THEN 'crown'
                        ELSE 'shield'
                    END,
                    description = CASE system_key
                        WHEN 'member' THEN 'Provides the standard role for signed-in users.'
                        WHEN 'admin' THEN 'Manages roles, users, and community operations.'
                        WHEN 'moderator' THEN 'Reviews reports and moderates community content and users.'
                    END
                WHERE system_key IN ('member', 'admin', 'moderator');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "description",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "roles");
        }
    }
}
