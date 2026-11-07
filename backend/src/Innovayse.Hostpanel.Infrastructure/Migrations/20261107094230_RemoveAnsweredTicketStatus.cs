using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnsweredTicketStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Answered ticket status is gone. The column keeps the status as text, so a stored
            // Answered would no longer read back as a known value: such tickets wait for the client,
            // which is what AwaitingReply means.
            migrationBuilder.Sql("UPDATE \"Tickets\" SET \"Status\" = 'AwaitingReply' WHERE \"Status\" = 'Answered';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which AwaitingReply tickets used to be Answered is not recorded, and
            // the old code reads AwaitingReply as it always did.
        }
    }
}
