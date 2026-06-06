using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketFeedbackAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FeedbackAt",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackComment",
                table: "tickets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackLeftBy",
                table: "tickets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_tags_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_tags_Name",
                table: "ticket_tags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_tags_TicketId_Name",
                table: "ticket_tags",
                columns: new[] { "TicketId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_tags");

            migrationBuilder.DropColumn(
                name: "FeedbackAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "FeedbackComment",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "FeedbackLeftBy",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "tickets");
        }
    }
}
