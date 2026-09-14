using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <summary>Adds the ISO 4217 code every invoice's amounts are in.</summary>
    /// <remarks>
    /// The column is NOT NULL and its default is <c>USD</c>, not the empty string EF generated.
    /// Every invoice that exists was raised in dollars — the only currency this product billed in
    /// before currencies became rows — so the backfill is the truth, not a guess. After the column
    /// exists nothing writes an empty code: the domain refuses it at creation.
    /// </remarks>
    public partial class AddInvoiceCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "invoices",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "invoices");
        }
    }
}
