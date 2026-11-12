using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductOptionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: true),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionGroups", x => x.Id);
                    table.CheckConstraint("CK_ProductOptionGroups_DisplayOrderNotNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ProductOptionGroups_QuantityRange", "(\"MinQuantity\" IS NULL AND \"MaxQuantity\" IS NULL) OR (\"MinQuantity\" >= 0 AND \"MaxQuantity\" >= 1 AND \"MaxQuantity\" >= \"MinQuantity\")");
                    table.ForeignKey(
                        name: "FK_ProductOptionGroups_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductOptionGroupId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PriceModifier = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SetupFeeModifier = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptions", x => x.Id);
                    table.CheckConstraint("CK_ProductOptions_DisplayOrderNotNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ProductOptions_ModifiersNotNegative", "\"PriceModifier\" >= 0 AND \"SetupFeeModifier\" >= 0");
                    table.CheckConstraint("CK_ProductOptions_PercentageAtMost100", "\"PriceType\" <> 'Percentage' OR (\"PriceModifier\" <= 100 AND \"SetupFeeModifier\" <= 100)");
                    table.ForeignKey(
                        name: "FK_ProductOptions_ProductOptionGroups_ProductOptionGroupId",
                        column: x => x.ProductOptionGroupId,
                        principalTable: "ProductOptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_DisplayOrder",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptions_ProductOptionGroupId_DisplayOrder",
                table: "ProductOptions",
                columns: new[] { "ProductOptionGroupId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductOptions");

            migrationBuilder.DropTable(
                name: "ProductOptionGroups");
        }
    }
}
