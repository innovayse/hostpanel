using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineEffectLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastRenewalOrderItemId",
                table: "Domains",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderItemId",
                table: "ClientServices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientServices_OrderItemId",
                table: "ClientServices",
                column: "OrderItemId",
                unique: true,
                filter: "\"OrderItemId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientServices_OrderItemId",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "LastRenewalOrderItemId",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "ClientServices");
        }
    }
}
