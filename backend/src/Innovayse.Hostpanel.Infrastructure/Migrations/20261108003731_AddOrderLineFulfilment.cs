using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineFulfilment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FulfilmentAttempts",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FulfilmentErrorCode",
                table: "OrderItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfilmentState",
                table: "OrderItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastFulfilmentAttemptAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "OrderItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfilmentAttempts",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FulfilmentErrorCode",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FulfilmentState",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "LastFulfilmentAttemptAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "OrderItems");
        }
    }
}
