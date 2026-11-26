using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityReads",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityReads", x => new { x.UserId, x.EventKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReads_UserId",
                table: "ActivityReads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityReads");
        }
    }
}
