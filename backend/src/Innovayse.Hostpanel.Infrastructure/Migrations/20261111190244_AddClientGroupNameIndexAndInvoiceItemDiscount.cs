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
            // the lowest Id keeps its name and every other one gets " (<Id>)" after it, cut short so the whole name
            // still fits the 100 character column. An Id is unique, so the suffixed names differ from each other.
            // A suffixed name that another group already carries (a group literally named "Name (Id)") is not taken:
            // that group is retried on the next pass with the pass number in the suffix, "Name (Id-2)", until no name
            // is shared.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    pass integer := 1;
                BEGIN
                    WHILE EXISTS (SELECT 1 FROM "ClientGroups" GROUP BY "Name" HAVING COUNT(*) > 1) LOOP
                        UPDATE "ClientGroups" g
                           SET "Name" = d."Candidate"
                          FROM (
                                SELECT t."Id", LEFT(t."Name", 100 - LENGTH(s."Suffix")) || s."Suffix" AS "Candidate"
                                  FROM (
                                        SELECT "Id", "Name", ROW_NUMBER() OVER (PARTITION BY "Name" ORDER BY "Id") AS "Position"
                                          FROM "ClientGroups"
                                       ) t
                                 CROSS JOIN LATERAL (
                                        SELECT ' (' || t."Id" || CASE WHEN pass > 1 THEN '-' || pass ELSE '' END || ')' AS "Suffix"
                                       ) s
                                 WHERE t."Position" > 1
                               ) d
                         WHERE d."Id" = g."Id"
                           AND NOT EXISTS (SELECT 1 FROM "ClientGroups" o WHERE o."Name" = d."Candidate");
                        pass := pass + 1;
                    END LOOP;
                END $$;
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
