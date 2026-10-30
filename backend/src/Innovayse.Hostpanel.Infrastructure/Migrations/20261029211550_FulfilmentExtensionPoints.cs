using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FulfilmentExtensionPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GracePeriodDays",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDigitalItems",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LapsedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "InvoiceItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDigital",
                table: "InvoiceItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "InvoiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductMetadata",
                table: "InvoiceItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "InvoiceItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                table: "Clients",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountProvider",
                table: "Clients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FulfilmentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfilmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FulfilmentRecords_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "\"Sku\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_GracePeriodDays",
                table: "Products",
                sql: "\"GracePeriodDays\" BETWEEN 0 AND 365");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ExternalAccountProvider_ExternalAccountId",
                table: "Clients",
                columns: new[] { "ExternalAccountProvider", "ExternalAccountId" },
                unique: true,
                filter: "\"ExternalAccountProvider\" IS NOT NULL AND \"ExternalAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clients_ExternalAccount",
                table: "Clients",
                sql: "(\"ExternalAccountProvider\" IS NULL) = (\"ExternalAccountId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FulfilmentRecords_InvoiceId_PluginId_Kind",
                table: "FulfilmentRecords",
                columns: new[] { "InvoiceId", "PluginId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfilmentRecords_Status_NextAttemptAt",
                table: "FulfilmentRecords",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FulfilmentRecords");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_GracePeriodDays",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ExternalAccountProvider_ExternalAccountId",
                table: "Clients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clients_ExternalAccount",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "GracePeriodDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HasDigitalItems",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LapsedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "IsDigital",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductMetadata",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ExternalAccountProvider",
                table: "Clients");
        }
    }
}
