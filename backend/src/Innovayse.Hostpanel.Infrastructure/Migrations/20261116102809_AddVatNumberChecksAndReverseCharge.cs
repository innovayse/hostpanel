using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVatNumberChecksAndReverseCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientVatNumber",
                table: "Invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReverseCharge",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VatAddress",
                table: "Clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VatCheckedAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatName",
                table: "Clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatStatus",
                table: "Clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientVatNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReverseCharge",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatAddress",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VatCheckedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VatName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VatStatus",
                table: "Clients");
        }
    }
}
