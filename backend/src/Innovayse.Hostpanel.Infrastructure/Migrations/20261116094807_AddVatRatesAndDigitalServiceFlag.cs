using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVatRatesAndDigitalServiceFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDigitalService",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDigitalService",
                table: "InvoiceItems",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VatRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatRates", x => x.Id);
                    table.CheckConstraint("CK_VatRates_RatePercent", "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VatRates_Country_Kind_ValidFrom",
                table: "VatRates",
                columns: new[] { "Country", "Kind", "ValidFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VatRates");

            migrationBuilder.DropColumn(
                name: "IsDigitalService",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsDigitalService",
                table: "InvoiceItems");
        }
    }
}
