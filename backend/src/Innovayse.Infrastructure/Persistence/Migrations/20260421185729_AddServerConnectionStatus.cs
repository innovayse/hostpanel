#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddServerConnectionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountsCount",
                table: "servers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "servers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastTestedAt",
                table: "servers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountsCount",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "LastTestedAt",
                table: "servers");
        }
    }
}
