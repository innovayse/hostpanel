using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationJobNewModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnouncementsImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AnnouncementsTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadsImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadsTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ExportAnnouncements",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportDownloads",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportNetworkIssues",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "NetworkIssuesImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NetworkIssuesTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementsImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "AnnouncementsTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "DownloadsImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "DownloadsTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportAnnouncements",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportDownloads",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportNetworkIssues",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "NetworkIssuesImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "NetworkIssuesTotal",
                table: "migration_jobs");
        }
    }
}
