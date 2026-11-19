using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstalledPlugins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstalledPlugins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PluginId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Signer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignerKeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PackageSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PackageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InstalledByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InstalledByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledPlugins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstalledPlugins_PluginId",
                table: "InstalledPlugins",
                column: "PluginId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstalledPlugins");
        }
    }
}
