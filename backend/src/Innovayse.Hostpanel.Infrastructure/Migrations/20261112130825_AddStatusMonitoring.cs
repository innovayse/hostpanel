using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveIncidentId",
                table: "StatusComponents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckUrl",
                table: "StatusComponents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "StatusComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FailureThreshold",
                table: "StatusComponents",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "MonitoringEnabled",
                table: "StatusComponents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StateOverride",
                table: "StatusComponents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // A component an operator had set by hand before the checks existed keeps that state: the manual state
            // is now the override, so the first automatic pass does not turn it back to Operational.
            migrationBuilder.Sql("UPDATE \"StatusComponents\" SET \"StateOverride\" = \"CurrentState\" WHERE \"CurrentState\" <> 'Operational';");

            migrationBuilder.CreateTable(
                name: "StatusChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponentId = table.Column<int>(type: "integer", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusChecks_StatusComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "StatusComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatusComponents_ActiveIncidentId",
                table: "StatusComponents",
                column: "ActiveIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusChecks_CheckedAt",
                table: "StatusChecks",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StatusChecks_ComponentId_CheckedAt",
                table: "StatusChecks",
                columns: new[] { "ComponentId", "CheckedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_StatusComponents_StatusIncidents_ActiveIncidentId",
                table: "StatusComponents",
                column: "ActiveIncidentId",
                principalTable: "StatusIncidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatusComponents_StatusIncidents_ActiveIncidentId",
                table: "StatusComponents");

            migrationBuilder.DropTable(
                name: "StatusChecks");

            migrationBuilder.DropIndex(
                name: "IX_StatusComponents_ActiveIncidentId",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "ActiveIncidentId",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "CheckUrl",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "FailureThreshold",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "MonitoringEnabled",
                table: "StatusComponents");

            migrationBuilder.DropColumn(
                name: "StateOverride",
                table: "StatusComponents");
        }
    }
}
