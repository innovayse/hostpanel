using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientGroupNameIndexAndInvoiceItemDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "InvoiceItems",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            // The unique index below refuses duplicate names, so groups that already share one are told apart first:
            // the lowest Id keeps its name and the others get " (2)", " (3)" and so on, cut short so the whole name
            // still fits the 100 character column.
            migrationBuilder.Sql("""
                UPDATE "ClientGroups" g
                   SET "Name" = LEFT(g."Name", 100 - LENGTH(' (' || d."Position" || ')')) || ' (' || d."Position" || ')'
                  FROM (
                        SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "Name" ORDER BY "Id") AS "Position"
                          FROM "ClientGroups"
                       ) d
                 WHERE d."Id" = g."Id"
                   AND d."Position" > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClientGroups_Name",
                table: "ClientGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientGroups_Name",
                table: "ClientGroups");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "InvoiceItems");
        }
    }
}
