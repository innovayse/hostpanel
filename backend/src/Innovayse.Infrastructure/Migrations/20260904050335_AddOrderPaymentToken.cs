using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the capability token that authorises paying for an order.
    /// </summary>
    /// <remarks>
    /// Additive: one new column, no data moved and nothing dropped, so it applies to a live
    /// database without downtime and <c>Down</c> is a clean reversal.
    /// </remarks>
    public partial class AddOrderPaymentToken : Migration
    {
        /// <summary>Adds the column and gives every existing order a token of its own.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentToken",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Rows that predate the column come out of AddColumn sharing the empty default, and a
            // token every order has in common is not a token. Order.MatchesPaymentToken already
            // refuses an empty one, so those orders are unpayable rather than universally payable
            // -- but that safety would rest entirely on a single guard clause in the domain, and
            // deleting that clause one day would silently turn "" into a master key for every
            // order that existed before this migration. Giving each row its own unguessable value
            // means the guard and the data are independently sufficient.
            //
            // Two uuids concatenated: 32 bytes of entropy, 64 hex characters, exactly the column
            // width. gen_random_uuid() is built into PostgreSQL 13+, so this needs no extension.
            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "PaymentToken" =
                    replace(gen_random_uuid()::text, '-', '') ||
                    replace(gen_random_uuid()::text, '-', '')
                WHERE "PaymentToken" = '';
                """);
        }

        /// <summary>Drops the column, taking every token with it.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentToken",
                table: "Orders");
        }
    }
}
