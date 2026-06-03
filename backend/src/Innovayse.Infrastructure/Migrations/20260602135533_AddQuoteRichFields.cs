using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRichFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNotes",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalText",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "quote_items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Taxed",
                table: "quote_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CustomerNotes",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProposalText",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "quote_items");

            migrationBuilder.DropColumn(
                name: "Taxed",
                table: "quote_items");
        }
    }
}
