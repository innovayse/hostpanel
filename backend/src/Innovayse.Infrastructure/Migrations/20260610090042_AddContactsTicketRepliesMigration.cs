#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddContactsTicketRepliesMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContactsImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContactsTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ExportContacts",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportTicketReplies",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketRepliesImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketRepliesTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactsImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ContactsTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportContacts",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportTicketReplies",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TicketRepliesImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TicketRepliesTotal",
                table: "migration_jobs");
        }
    }
}
