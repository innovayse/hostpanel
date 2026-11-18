using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductOrderLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientServices_ProductId",
                table: "ClientServices");

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveServices",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnePerClient",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_MaxActiveServices",
                table: "Products",
                sql: "\"MaxActiveServices\" IS NULL OR \"MaxActiveServices\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientServices_ProductId_Status",
                table: "ClientServices",
                columns: new[] { "ProductId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_MaxActiveServices",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ClientServices_ProductId_Status",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "MaxActiveServices",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OnePerClient",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_ClientServices_ProductId",
                table: "ClientServices",
                column: "ProductId");
        }
    }
}
