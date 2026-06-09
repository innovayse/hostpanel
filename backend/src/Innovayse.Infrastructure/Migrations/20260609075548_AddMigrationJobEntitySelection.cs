using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationJobEntitySelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExportClients",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportDomains",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportInvoices",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportServices",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportTickets",
                table: "migration_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPingAt",
                table: "migration_jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportClients",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportDomains",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportInvoices",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportServices",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "ExportTickets",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "LastPingAt",
                table: "migration_jobs");
        }
    }
}
