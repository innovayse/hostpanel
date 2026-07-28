using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRepliedClosedEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "email_templates",
                columns: new[] { "Id", "Body", "Description", "IsActive", "Slug", "Subject" },
                values: new object[,]
                {
                    { 6, "<p>There's a new reply on your support ticket.</p>", "Sent when a reply is added to a ticket", true, "ticket-replied", "New Reply on Ticket #{{ticket.id}}: {{ticket.subject}}" },
                    { 7, "<p>Your support ticket \"{{ticket.subject}}\" has been closed. We'd love your feedback.</p>", "Sent when a ticket is closed", true, "ticket-closed", "Support Ticket #{{ticket.id}} Closed" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "email_templates",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "email_templates",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
