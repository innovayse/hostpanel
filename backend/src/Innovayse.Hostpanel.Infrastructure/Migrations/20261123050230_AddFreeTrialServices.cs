using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeTrialServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrialAutoConvert",
                table: "ClientServices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsAt",
                table: "ClientServices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrialGracePeriodDays",
                table: "ClientServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialPaymentDueAt",
                table: "ClientServices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrialReminderDays",
                table: "ClientServices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrialStatus",
                table: "ClientServices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientProductTrials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProductTrials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientServices_TrialStatus_TrialEndsAt",
                table: "ClientServices",
                columns: new[] { "TrialStatus", "TrialEndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientProductTrials_ClientId_ProductId",
                table: "ClientProductTrials",
                columns: new[] { "ClientId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientProductTrials");

            migrationBuilder.DropIndex(
                name: "IX_ClientServices_TrialStatus_TrialEndsAt",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialAutoConvert",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialGracePeriodDays",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialPaymentDueAt",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialReminderDays",
                table: "ClientServices");

            migrationBuilder.DropColumn(
                name: "TrialStatus",
                table: "ClientServices");
        }
    }
}
