using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTrials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrialAutoConvert",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<bool>(
                name: "TrialEnabled",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TrialGracePeriodDays",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "TrialReminderDays",
                table: "Products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "3,1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_TrialDays",
                table: "Products",
                sql: "\"TrialDays\" BETWEEN 1 AND 365");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_TrialGracePeriodDays",
                table: "Products",
                sql: "\"TrialGracePeriodDays\" BETWEEN 0 AND 30");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_TrialDays",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_TrialGracePeriodDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrialAutoConvert",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrialEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrialGracePeriodDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrialReminderDays",
                table: "Products");
        }
    }
}
