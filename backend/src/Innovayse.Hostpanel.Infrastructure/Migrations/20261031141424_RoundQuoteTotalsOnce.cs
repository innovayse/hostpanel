using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Recalculates the stored <c>Total</c> of every quote that has lines. Before this change a quote total was
    /// the sum of per-line values, and a total saved before the rounding fix kept more decimals than its currency
    /// allows; the quote lists read that stored value. The new total is the unrounded sum of the discounted
    /// lines, rounded once to the currency's minor unit (half away from zero, as <c>CurrencyCode.RoundToMinorUnits</c>
    /// does), exactly how a quote now computes it. The zero and three decimal currency lists mirror
    /// <c>CurrencyCode</c>; every other currency has two. Quotes without lines keep their total, and only rows
    /// whose total would change are written.
    /// </summary>
    public partial class RoundQuoteTotalsOnce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Quotes" AS q
                SET "Total" = s."Rounded"
                FROM (
                    SELECT q2."Id",
                           ROUND(
                               SUM(i."UnitPrice" * i."Quantity" * (1 - i."DiscountPercent" / 100)),
                               CASE
                                   WHEN UPPER(q2."Currency") IN ('BIF', 'CLP', 'DJF', 'GNF', 'ISK', 'JPY', 'KMF', 'KRW', 'PYG',
                                                                 'RWF', 'UGX', 'UYI', 'VND', 'VUV', 'XAF', 'XOF', 'XPF') THEN 0
                                   WHEN UPPER(q2."Currency") IN ('BHD', 'IQD', 'JOD', 'KWD', 'LYD', 'OMR', 'TND') THEN 3
                                   ELSE 2
                               END) AS "Rounded"
                    FROM "Quotes" AS q2
                    INNER JOIN "QuoteItems" AS i ON i."QuoteId" = q2."Id"
                    GROUP BY q2."Id", q2."Currency"
                ) AS s
                WHERE q."Id" = s."Id" AND q."Total" <> s."Rounded";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the rounded totals are the correct values, and the earlier unrounded ones
            // cannot be told apart from totals the application saved afterwards.
        }
    }
}
