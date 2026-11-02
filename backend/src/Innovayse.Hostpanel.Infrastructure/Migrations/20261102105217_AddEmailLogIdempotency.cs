using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Lets a queued mail be delivered once per recipient: <c>IdempotencyKey</c> names the delivery and is unique
    /// when set, <c>State</c> records whether it is being sent, was sent or failed, and the row version lets two
    /// deliveries that both try to take over a stale claim see each other. Every existing row is a finished
    /// delivery, so its state is backfilled from <c>Success</c>: delivered rows become Sent (2), the rest Failed (3).
    /// </summary>
    public partial class AddEmailLogIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "EmailLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "EmailLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows were written by the unkeyed path, which records only the outcome.
            migrationBuilder.Sql(
                """
                UPDATE "EmailLogs" SET "State" = CASE WHEN "Success" THEN 2 ELSE 3 END WHERE "State" = 0;
                """);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "EmailLogs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_IdempotencyKey",
                table: "EmailLogs",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailLogs_IdempotencyKey",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "State",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "EmailLogs");
        }
    }
}
