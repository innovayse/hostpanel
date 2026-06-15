using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationJobSkippedCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnouncementsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClientsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContactsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DomainsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DownloadsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvoicesSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KnowledgebaseSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NetworkIssuesSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuotesSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ServicesSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketRepliesSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransactionsSkipped",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ClientsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ContactsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "DomainsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "DownloadsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "InvoicesSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "KnowledgebaseSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "NetworkIssuesSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "OrdersSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ProductsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "QuotesSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ServicesSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TicketRepliesSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TicketsSkipped",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TransactionsSkipped",
                table: "migration_jobs");
        }
    }
}
