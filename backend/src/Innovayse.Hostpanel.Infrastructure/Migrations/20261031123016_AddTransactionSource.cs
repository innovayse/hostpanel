using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the <c>Source</c> column to <c>Transactions</c>, fills it for every existing row, checks that no
    /// two gateway payment rows share a method and a reference, and only then creates the unique index
    /// over those rows. The backfill reads what the rows themselves say, in this order: a row linked to an
    /// invoice that was imported is an Import; a row that took money in under a gateway's name (not Manual,
    /// Bank Transfer, Credit Balance or No Charge), with a reference, and with a description one of the
    /// gateway paths writes, is a GatewayPayment; the matching row that paid money out is a GatewayRefund;
    /// every other row is Manual. History cannot tell a payment staff recorded under a gateway's name from
    /// the gateway's own, and cannot find an import that is linked to no invoice, so those stay on the side
    /// the rules above put them. <c>scripts/check-duplicate-gateway-payments.sh</c> lists the rows that would
    /// stop the index before the deploy.
    /// </summary>
    public partial class AddTransactionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every existing row starts as a manual entry, then the rows that say otherwise are moved.
            migrationBuilder.Sql("UPDATE \"Transactions\" SET \"Source\" = 'Manual';");

            // Rows copied in from another system are linked to an invoice that carries the source's id.
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" AS t
                SET "Source" = 'Import'
                WHERE EXISTS (SELECT 1 FROM "Invoices" AS i WHERE i."Id" = t."InvoiceId" AND i."ExternalId" IS NOT NULL);
                """);

            // Money a gateway took: the settlement, the credit of a surplus and the hand-back of a payment
            // the invoice could not record all write one of these three descriptions.
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" AS t
                SET "Source" = 'GatewayPayment'
                WHERE t."Source" = 'Manual'
                  AND t."TransactionId" <> ''
                  AND t."AmountIn" > 0
                  AND t."PaymentMethod" NOT IN ('Manual', 'Bank Transfer', 'Credit Balance', 'No Charge')
                  AND (t."Description" LIKE 'Invoice #% Payment'
                       OR t."Description" LIKE 'Gateway payment for Invoice #%'
                       OR t."Description" LIKE 'Card payment for Invoice #%');
                """);

            // Money a gateway returned.
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" AS t
                SET "Source" = 'GatewayRefund'
                WHERE t."Source" = 'Manual'
                  AND t."AmountOut" > 0
                  AND t."AmountIn" = 0
                  AND t."PaymentMethod" NOT IN ('Manual', 'Bank Transfer', 'Credit Balance', 'No Charge')
                  AND t."Description" LIKE 'Invoice #% Refund';
                """);

            // Stops with the offending rows named, instead of the index failing with no list. The same
            // groups are what scripts/check-duplicate-gateway-payments.sh prints before the deploy.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE duplicates text;
                BEGIN
                    SELECT string_agg(
                        format('%s / %s (ledger rows %s)', d."PaymentMethod", d."TransactionId", d."Ids"), '; ' ORDER BY d."FirstId")
                    INTO duplicates
                    FROM (
                        SELECT "PaymentMethod", "TransactionId", min("Id") AS "FirstId", string_agg("Id"::text, ', ' ORDER BY "Id") AS "Ids"
                        FROM "Transactions"
                        WHERE "Source" = 'GatewayPayment'
                        GROUP BY "PaymentMethod", "TransactionId"
                        HAVING count(*) > 1
                    ) AS d;

                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'Gateway payment rows share a payment method and reference, so the unique index cannot be created. Resolve them first (scripts/check-duplicate-gateway-payments.sh lists them): %', duplicates;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentMethod_TransactionId_GatewayPayment",
                table: "Transactions",
                columns: new[] { "PaymentMethod", "TransactionId" },
                unique: true,
                filter: "\"Source\" = 'GatewayPayment'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_PaymentMethod_TransactionId_GatewayPayment",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Transactions");
        }
    }
}
