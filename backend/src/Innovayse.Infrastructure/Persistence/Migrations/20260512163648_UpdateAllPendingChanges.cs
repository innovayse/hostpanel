using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "domains");

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "domains",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DnsManagement",
                table: "domains",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailForwarding",
                table: "domains",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPaymentAmount",
                table: "domains",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "domains",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "domains",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Register");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "domains",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionCode",
                table: "domains",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecurringAmount",
                table: "domains",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionId",
                table: "domains",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "domain_reminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DomainId = table.Column<int>(type: "integer", nullable: false),
                    ReminderType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SentTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_domain_reminders_domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_forwarding_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DomainId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Destination = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_forwarding_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_forwarding_rules_domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_domain_reminders_DomainId",
                table: "domain_reminders",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_email_forwarding_rules_DomainId",
                table: "email_forwarding_rules",
                column: "DomainId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_reminders");

            migrationBuilder.DropTable(
                name: "email_forwarding_rules");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "DnsManagement",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "EmailForwarding",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "FirstPaymentAmount",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "PromotionCode",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "RecurringAmount",
                table: "domains");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "domains");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "domains",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
