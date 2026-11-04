using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Saved payment methods and automatic renewal charges: <c>GatewayCustomers</c> holds a client's account at each payment
    /// gateway module (the single <c>Clients.CardGatewayCustomerId</c> column moves into it as the "stripe" module and is
    /// dropped), <c>CardChargeConsents</c> records the client's agreement per saved method, and <c>InvoicePaymentClaims</c>
    /// holds the exclusive right to charge one invoice. A customer id shared by two clients stops the migration before any
    /// change, because the new table lets one gateway account belong to one client only.
    /// </summary>
    public partial class AddSavedMethodCharging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data the new unique index cannot hold stops the migration before any change, so an operator resolves
            // those rows by hand instead of losing them. The check names the duplicated customer ids.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    SharedCustomers text;
                BEGIN
                    SELECT string_agg(quote_literal(d."CardGatewayCustomerId"), ', ' ORDER BY d."CardGatewayCustomerId")
                    INTO SharedCustomers
                    FROM (
                        SELECT "CardGatewayCustomerId" FROM "Clients"
                        WHERE "CardGatewayCustomerId" IS NOT NULL
                        GROUP BY "CardGatewayCustomerId" HAVING count(*) > 1
                    ) d;
                    IF SharedCustomers IS NOT NULL THEN
                        RAISE EXCEPTION 'Clients share a card gateway customer id (%); resolve them before migrating.', SharedCustomers;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "CardChargeConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GatewaySessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    GatewayMethodRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TextVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                });

            // Every existing customer id is a Stripe customer: the built-in gateway was the only one that held cards.
            migrationBuilder.Sql(
                """
                INSERT INTO "GatewayCustomers" ("ClientId", "GatewayModuleId", "CustomerRef", "CreatedAt")
                SELECT "Id", 'stripe', "CardGatewayCustomerId", now()
                FROM "Clients"
                WHERE "CardGatewayCustomerId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "CardGatewayCustomerId",
                table: "Clients");

            migrationBuilder.CreateTable(
                name: "InvoicePaymentClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    GatewayModuleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                name: "IX_CardChargeConsents_ClientId_GatewayModuleId_GatewayMethodRef",
                table: "CardChargeConsents",
                columns: new[] { "ClientId", "GatewayModuleId", "GatewayMethodRef" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_ClientId_Status",
                table: "CardChargeConsents",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CardChargeConsents_GatewayModuleId_GatewaySessionId",
                table: "CardChargeConsents",
                columns: new[] { "GatewayModuleId", "GatewaySessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCustomers_ClientId_GatewayModuleId",
                table: "GatewayCustomers",
                columns: new[] { "ClientId", "GatewayModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCustomers_GatewayModuleId_CustomerRef",
                table: "GatewayCustomers",
                columns: new[] { "GatewayModuleId", "CustomerRef" },
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
            migrationBuilder.AddColumn<string>(
                name: "CardGatewayCustomerId",
                table: "Clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // The column holds one customer id, so only the built-in gateway's account goes back.
            migrationBuilder.Sql(
                """
                UPDATE "Clients" c SET "CardGatewayCustomerId" = g."CustomerRef"
                FROM "GatewayCustomers" g
                WHERE g."ClientId" = c."Id" AND g."GatewayModuleId" = 'stripe' AND length(g."CustomerRef") <= 50;
                """);

            migrationBuilder.DropTable(
                name: "CardChargeConsents");

            migrationBuilder.DropTable(
                name: "GatewayCustomers");

            migrationBuilder.DropTable(
                name: "InvoicePaymentClaims");
        }
    }
}
