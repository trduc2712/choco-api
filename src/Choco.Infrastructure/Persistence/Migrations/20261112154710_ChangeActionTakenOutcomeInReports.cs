using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choco.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeActionTakenOutcomeInReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE reports
                SET outcome = CASE target_type WHEN 'Post' THEN 'ContentRemoved' ELSE 'UserSuspended' END
                WHERE outcome = 'ActionTaken';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE reports
                SET outcome = 'ActionTaken'
                WHERE outcome IN ('ContentRemoved', 'UserSuspended');
                """);
        }
    }
}
