using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TightenProductOptionQuantityRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_QuantityRange",
                table: "ProductOptionGroups");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_QuantityRange",
                table: "ProductOptionGroups",
                sql: "(\"MinQuantity\" IS NULL AND \"MaxQuantity\" IS NULL) OR (\"MinQuantity\" IS NOT NULL AND \"MaxQuantity\" IS NOT NULL AND \"MinQuantity\" >= 0 AND \"MaxQuantity\" >= 1 AND \"MaxQuantity\" >= \"MinQuantity\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductOptionGroups_QuantityRange",
                table: "ProductOptionGroups");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductOptionGroups_QuantityRange",
                table: "ProductOptionGroups",
                sql: "(\"MinQuantity\" IS NULL AND \"MaxQuantity\" IS NULL) OR (\"MinQuantity\" >= 0 AND \"MaxQuantity\" >= 1 AND \"MaxQuantity\" >= \"MinQuantity\")");
        }
    }
}
