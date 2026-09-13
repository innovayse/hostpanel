using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Numeric = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Suffix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    RateToBase = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    IsBase = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_currencies_Code",
                table: "currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_currencies_IsBase",
                table: "currencies",
                column: "IsBase",
                unique: true,
                filter: "\"IsBase\" = true");

            // Seed the two currencies this store runs today. USD is the base because every
            // existing price is a dollar figure; the AMD rate is the one the storefront carried
            // in stores/cart.ts, the only place a rate existed before this table.
            migrationBuilder.Sql(
                """
                INSERT INTO "currencies" ("Code","Numeric","Prefix","Suffix","Decimals","RateToBase","IsBase","IsEnabled")
                VALUES ('USD','840','$','',2,1,true,true),
                       ('AMD','051','',' ֏',0,0.00256410,false,true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currencies");
        }
    }
}
