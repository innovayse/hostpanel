using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatusMaintenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusMaintenances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusMaintenanceComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaintenanceId = table.Column<int>(type: "integer", nullable: false),
                    ComponentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusMaintenanceComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusMaintenanceComponents_StatusComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "StatusComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StatusMaintenanceComponents_StatusMaintenances_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "StatusMaintenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatusMaintenanceComponents_ComponentId",
                table: "StatusMaintenanceComponents",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusMaintenanceComponents_MaintenanceId_ComponentId",
                table: "StatusMaintenanceComponents",
                columns: new[] { "MaintenanceId", "ComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusMaintenances_ScheduledEnd",
                table: "StatusMaintenances",
                column: "ScheduledEnd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatusMaintenanceComponents");

            migrationBuilder.DropTable(
                name: "StatusMaintenances");
        }
    }
}
