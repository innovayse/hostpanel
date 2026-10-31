using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardAutoCharging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardChargeConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    GatewayPaymentMethodId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TextVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardChargeConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardChargeConsents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePaymentClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GatewayIdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePaymentClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePaymentClaims_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CardGatewayCustomerId",
                table: "Clients",
                column: "CardGatewayCustomerId",
                unique: true,
                filter: "\"CardGatewayCustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_ClientId_GatewayPaymentMethodId",
                table: "CardChargeConsents",
                columns: new[] { "ClientId", "GatewayPaymentMethodId" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_ClientId_Status",
                table: "CardChargeConsents",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_GatewayIntentId",
                table: "CardChargeConsents",
                column: "GatewayIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_GatewayIdempotencyKey",
                table: "InvoicePaymentClaims",
                column: "GatewayIdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_InvoiceId",
                table: "InvoicePaymentClaims",
                column: "InvoiceId",
                unique: true,
                filter: "\"Outcome\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardChargeConsents");

            migrationBuilder.DropTable(
                name: "InvoicePaymentClaims");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CardGatewayCustomerId",
                table: "Clients");
        }
    }
}
