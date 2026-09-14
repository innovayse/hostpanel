using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Cycle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_prices_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_ProductId_CurrencyCode_Cycle",
                table: "product_prices",
                columns: new[] { "ProductId", "CurrencyCode", "Cycle" },
                unique: true);

            // Every existing product price is a USD figure. Copy both cycles into the new table
            // so nothing changes for a client on the day this deploys; the legacy columns stay
            // one release for rollback and are no longer written.
            migrationBuilder.Sql(
                """
                INSERT INTO "product_prices" ("ProductId","CurrencyCode","Cycle","Amount")
                SELECT "Id", 'USD', 'Monthly', "MonthlyPrice" FROM "products"
                UNION ALL
                SELECT "Id", 'USD', 'Annual',  "AnnualPrice"  FROM "products";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_prices");
        }
    }
}
