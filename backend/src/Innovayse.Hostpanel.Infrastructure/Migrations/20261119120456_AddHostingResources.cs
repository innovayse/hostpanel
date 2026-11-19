using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHostingResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "HostingResourceSequence");

            migrationBuilder.AddColumn<long>(
                name: "HostingResourceRevision",
                table: "ClientServices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ServiceDatabases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"HostingResourceSequence\"')"),
                    ClientServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    AccountUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedToPluginId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatabaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDatabases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDatabases_ClientServices_ClientServiceId",
                        column: x => x.ClientServiceId,
                        principalTable: "ClientServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceWebsites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('\"HostingResourceSequence\"')"),
                    ClientServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    AccountUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedToPluginId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WebsiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    DocumentRoot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PhpVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DocumentRootMayBeLink = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceWebsites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceWebsites_ClientServices_ClientServiceId",
                        column: x => x.ClientServiceId,
                        principalTable: "ClientServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDatabases_ClientServiceId",
                table: "ServiceDatabases",
                column: "ClientServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDatabases_ClientServiceId_IssuedToPluginId_Label",
                table: "ServiceDatabases",
                columns: new[] { "ClientServiceId", "IssuedToPluginId", "Label" },
                unique: true,
                filter: "\"State\" <> 'Removed'");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDatabases_DatabaseId",
                table: "ServiceDatabases",
                column: "DatabaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDatabases_ServerId",
                table: "ServiceDatabases",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWebsites_ClientServiceId",
                table: "ServiceWebsites",
                column: "ClientServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWebsites_ClientServiceId_Hostname",
                table: "ServiceWebsites",
                columns: new[] { "ClientServiceId", "Hostname" },
                unique: true,
                filter: "\"State\" <> 'Removed'");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWebsites_ServerId",
                table: "ServiceWebsites",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWebsites_WebsiteId",
                table: "ServiceWebsites",
                column: "WebsiteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDatabases");

            migrationBuilder.DropTable(
                name: "ServiceWebsites");

            migrationBuilder.DropColumn(
                name: "HostingResourceRevision",
                table: "ClientServices");

            migrationBuilder.DropSequence(
                name: "HostingResourceSequence");
        }
    }
}
