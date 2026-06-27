using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTldConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tld_configs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tld = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegistrarModule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SellCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Categories = table.Column<string>(type: "jsonb", nullable: false),
                    CostRegister = table.Column<string>(type: "jsonb", nullable: false),
                    CostTransfer = table.Column<string>(type: "jsonb", nullable: false),
                    CostRenew = table.Column<string>(type: "jsonb", nullable: false),
                    SellRegister = table.Column<string>(type: "jsonb", nullable: false),
                    SellTransfer = table.Column<string>(type: "jsonb", nullable: false),
                    SellRenew = table.Column<string>(type: "jsonb", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tld_configs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tld_configs_IsEnabled",
                table: "tld_configs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_tld_configs_Tld",
                table: "tld_configs",
                column: "Tld",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tld_configs");
        }
    }
}
