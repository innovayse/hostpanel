using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainLineRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRenewalOrderItemId",
                table: "Domains");

            migrationBuilder.CreateTable(
                name: "DomainLineRenewals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DomainId = table.Column<int>(type: "integer", nullable: false),
                    OrderItemId = table.Column<int>(type: "integer", nullable: false),
                    RenewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainLineRenewals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DomainLineRenewals_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainLineRenewals_DomainId",
                table: "DomainLineRenewals",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainLineRenewals_OrderItemId",
                table: "DomainLineRenewals",
                column: "OrderItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainLineRenewals");

            migrationBuilder.AddColumn<int>(
                name: "LastRenewalOrderItemId",
                table: "Domains",
                type: "integer",
                nullable: true);
        }
    }
}
