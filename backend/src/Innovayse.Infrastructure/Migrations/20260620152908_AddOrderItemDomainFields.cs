#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddOrderItemDomainFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DomainAction",
                table: "OrderItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EppCode",
                table: "OrderItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Years",
                table: "OrderItems",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DomainAction",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "EppCode",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Years",
                table: "OrderItems");
        }
    }
}
