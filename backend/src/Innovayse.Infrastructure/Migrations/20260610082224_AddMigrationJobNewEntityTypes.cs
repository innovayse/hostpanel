using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationJobNewEntityTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExportKnowledgebase",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportOrders",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportProducts",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportQuotes",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportTransactions",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "KnowledgebaseImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KnowledgebaseTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdersTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductsImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductsTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuotesImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuotesTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransactionsImported",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransactionsTotal",
                table: "migration_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportKnowledgebase",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportOrders",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportProducts",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportQuotes",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportTransactions",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "KnowledgebaseImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "KnowledgebaseTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "OrdersImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "OrdersTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ProductsImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ProductsTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "QuotesImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "QuotesTotal",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TransactionsImported",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TransactionsTotal",
                table: "migration_jobs");
        }
    }
}
