using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoredCredentialsImportIdsAndRefundClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PendingRefundAmount",
                table: "Invoices",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingRefundId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "ClientServices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardChargeConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MethodRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsentTextVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardChargeConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardChargeConsents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatewayCustomers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePaymentClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MethodRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActionUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePaymentClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePaymentClaims_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoicePaymentClaims_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientServices_ClientId_ExternalId",
                table: "ClientServices",
                columns: new[] { "ClientId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_ClientId_GatewayModuleId",
                table: "CardChargeConsents",
                columns: new[] { "ClientId", "GatewayModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_GatewayModuleId_SessionId",
                table: "CardChargeConsents",
                columns: new[] { "GatewayModuleId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCustomers_ClientId_GatewayModuleId",
                table: "GatewayCustomers",
                columns: new[] { "ClientId", "GatewayModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_ClientId",
                table: "InvoicePaymentClaims",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_IdempotencyKey",
                table: "InvoicePaymentClaims",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_InvoiceId_Blocking",
                table: "InvoicePaymentClaims",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\" IN ('Open', 'ActionRequired', 'Pending', 'Held')");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePaymentClaims_Status_CreatedAt",
                table: "InvoicePaymentClaims",
                columns: new[] { "Status", "CreatedAt" });

            // Legacy card customers move to the per-gateway table before the column goes: the only gateway that
            // wrote the column was the Stripe one, so each non-empty value becomes that client's Stripe customer.
            migrationBuilder.Sql(
                """
                INSERT INTO "GatewayCustomers" ("ClientId", "GatewayModuleId", "CustomerRef", "CreatedAt")
                SELECT "Id", 'stripe', "CardGatewayCustomerId", now()
                FROM "Clients"
                WHERE "CardGatewayCustomerId" IS NOT NULL AND "CardGatewayCustomerId" <> ''
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "CardGatewayCustomerId",
                table: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardGatewayCustomerId",
                table: "Clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Clients" AS c
                SET "CardGatewayCustomerId" = g."CustomerRef"
                FROM "GatewayCustomers" AS g
                WHERE g."ClientId" = c."Id" AND g."GatewayModuleId" = 'stripe';
                """);

            migrationBuilder.DropTable(
                name: "CardChargeConsents");

            migrationBuilder.DropTable(
                name: "GatewayCustomers");

            migrationBuilder.DropTable(
                name: "InvoicePaymentClaims");

            migrationBuilder.DropIndex(
                name: "IX_ClientServices_ClientId_ExternalId",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "PendingRefundAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PendingRefundId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "ClientServices");
        }
    }
}
