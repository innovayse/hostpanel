using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PluginDataStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PluginDataEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PluginId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginDataEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PluginDataScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PluginId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    StoredChars = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginDataScopes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PluginDataEntries_PluginId_ClientRef_Key",
                table: "PluginDataEntries",
                columns: new[] { "PluginId", "ClientRef", "Key" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_PluginDataScopes_PluginId_ClientRef",
                table: "PluginDataScopes",
                columns: new[] { "PluginId", "ClientRef" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PluginDataEntries");

            migrationBuilder.DropTable(
                name: "PluginDataScopes");
        }
    }
}
