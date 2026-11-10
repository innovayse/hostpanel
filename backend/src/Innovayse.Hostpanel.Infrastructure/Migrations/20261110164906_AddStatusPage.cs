using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatusComponentGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusComponentGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusComponents_StatusComponentGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StatusComponentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusIncidentUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusIncidentUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusIncidentUpdates_StatusIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "StatusIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatusComponentGroups_DisplayOrder",
                table: "StatusComponentGroups",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_StatusComponents_GroupId_DisplayOrder",
                table: "StatusComponents",
                columns: new[] { "GroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusIncidents_StartedAt",
                table: "StatusIncidents",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StatusIncidents_State",
                table: "StatusIncidents",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_StatusIncidentUpdates_IncidentId_CreatedAt",
                table: "StatusIncidentUpdates",
                columns: new[] { "IncidentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatusComponents");

            migrationBuilder.DropTable(
                name: "StatusIncidentUpdates");

            migrationBuilder.DropTable(
                name: "StatusComponentGroups");

            migrationBuilder.DropTable(
                name: "StatusIncidents");
        }
    }
}
