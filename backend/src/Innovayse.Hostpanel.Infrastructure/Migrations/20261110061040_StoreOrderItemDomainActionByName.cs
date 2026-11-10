using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreOrderItemDomainActionByName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The schema does not change: OrderItems.DomainAction stays a text column. Only the stored
            // values move from the lower-case texts to the DomainAction member names, which EF cannot
            // generate. A value that is none of the three known actions in any casing stops the
            // migration before any row changes and is named, so the operator resolves it by hand
            // instead of the order line silently becoming a different action.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    UnknownActions text;
                BEGIN
                    SELECT string_agg(DISTINCT quote_literal("DomainAction"), ', ')
                    INTO UnknownActions
                    FROM "OrderItems"
                    WHERE "DomainAction" IS NOT NULL
                      AND lower(btrim("DomainAction")) NOT IN ('register', 'transfer', 'renew');
                    IF UnknownActions IS NOT NULL THEN
                        RAISE EXCEPTION 'OrderItems hold domain actions other than register, transfer or renew (%); resolve those rows before migrating.', UnknownActions;
                    END IF;
                END $$;

                UPDATE "OrderItems"
                SET "DomainAction" = CASE lower(btrim("DomainAction"))
                    WHEN 'register' THEN 'Register'
                    WHEN 'transfer' THEN 'Transfer'
                    WHEN 'renew' THEN 'Renew'
                END
                WHERE "DomainAction" IS NOT NULL
                  AND "DomainAction" NOT IN ('Register', 'Transfer', 'Renew');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the lower-case texts the previous release reads.
            migrationBuilder.Sql(
                """
                UPDATE "OrderItems"
                SET "DomainAction" = lower("DomainAction")
                WHERE "DomainAction" IS NOT NULL;
                """);
        }
    }
}
