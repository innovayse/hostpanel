using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiskUsageStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disk_usage_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerName = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DiskUsageMb = table.Column<long>(type: "bigint", nullable: false),
                    DiskLimitMb = table.Column<long>(type: "bigint", nullable: false),
                    BwUsageMb = table.Column<long>(type: "bigint", nullable: false),
                    BwLimitMb = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disk_usage_stats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disk_usage_stats_Domain",
                table: "disk_usage_stats",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disk_usage_stats");
        }
    }
}
