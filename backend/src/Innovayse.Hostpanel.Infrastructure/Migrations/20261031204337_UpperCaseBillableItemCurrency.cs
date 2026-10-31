using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Upper-cases the stored <c>Currency</c> of every billable item. A currency code is kept in upper case
    /// everywhere else, and a billable item saved with a lower case or padded code would not group with the
    /// other items of its currency when the uninvoiced totals are summed per currency. Only rows whose code
    /// would change are written.
    /// </summary>
    public partial class UpperCaseBillableItemCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "BillableItems" SET "Currency" = UPPER(TRIM("Currency")) WHERE "Currency" <> UPPER(TRIM("Currency"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the upper case code is the correct value, and the original letter case
            // cannot be told apart from codes the application saved afterwards.
        }
    }
}
