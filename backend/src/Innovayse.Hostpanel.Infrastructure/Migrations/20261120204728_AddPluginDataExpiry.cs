using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginDataExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "PluginDataEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginDataEntries_ExpiresAt",
                table: "PluginDataEntries",
                column: "ExpiresAt",
                filter: "\"ExpiresAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PluginDataEntries_ExpiresAt",
                table: "PluginDataEntries");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PluginDataEntries");
        }
    }
}
