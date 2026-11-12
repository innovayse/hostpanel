using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BundleId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductBundles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IncludesDomain = table.Column<bool>(type: "boolean", nullable: false),
                    DomainYears = table.Column<int>(type: "integer", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBundles", x => x.Id);
                    table.CheckConstraint("CK_ProductBundles_Discount", "(\"DiscountType\" = 'None' AND \"DiscountAmount\" = 0 AND \"DiscountCurrency\" IS NULL) OR (\"DiscountType\" = 'Percentage' AND \"DiscountAmount\" BETWEEN 0 AND 100 AND \"DiscountCurrency\" IS NULL) OR (\"DiscountType\" = 'Fixed' AND \"DiscountAmount\" >= 0 AND \"DiscountCurrency\" IS NOT NULL)");
                    table.CheckConstraint("CK_ProductBundles_DomainTerm", "(\"IncludesDomain\" AND \"DomainYears\" BETWEEN 1 AND 10) OR (NOT \"IncludesDomain\" AND \"DomainYears\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ProductBundleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BundleId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBundleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBundleItems_ProductBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "ProductBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductBundleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BundleId",
                table: "Orders",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBundleItems_BundleId_ProductId",
                table: "ProductBundleItems",
                columns: new[] { "BundleId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBundleItems_ProductId",
                table: "ProductBundleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBundles_IsActive_SortOrder",
                table: "ProductBundles",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ProductBundles_BundleId",
                table: "Orders",
                column: "BundleId",
                principalTable: "ProductBundles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ProductBundles_BundleId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "ProductBundleItems");

            migrationBuilder.DropTable(
                name: "ProductBundles");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BundleId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BundleId",
                table: "Orders");
        }
    }
}
