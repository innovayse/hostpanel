using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    FromProductId = table.Column<int>(type: "integer", nullable: false),
                    ToProductId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProrationAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NewRecurringAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    InvoiceId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceChangeRequests_ClientServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "ClientServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceChangeRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceChangeRequests_Products_FromProductId",
                        column: x => x.FromProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceChangeRequests_Products_ToProductId",
                        column: x => x.ToProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_ClientId",
                table: "ServiceChangeRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_FromProductId",
                table: "ServiceChangeRequests",
                column: "FromProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_InvoiceId",
                table: "ServiceChangeRequests",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_ServiceId",
                table: "ServiceChangeRequests",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_ServiceId_Open",
                table: "ServiceChangeRequests",
                column: "ServiceId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_Status",
                table: "ServiceChangeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChangeRequests_ToProductId",
                table: "ServiceChangeRequests",
                column: "ToProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceChangeRequests");
        }
    }
}
