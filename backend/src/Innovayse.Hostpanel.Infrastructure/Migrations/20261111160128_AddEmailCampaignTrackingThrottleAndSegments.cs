using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCampaignTrackingThrottleAndSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "EmailCampaignRecipients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstClickedAt",
                table: "EmailCampaignRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstOpenedAt",
                table: "EmailCampaignRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenCount",
                table: "EmailCampaignRecipients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TrackingTokenHash",
                table: "EmailCampaignRecipients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_SentAt",
                table: "EmailCampaignRecipients",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_TrackingTokenHash",
                table: "EmailCampaignRecipients",
                column: "TrackingTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignRecipients_SentAt",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropIndex(
                name: "IX_EmailCampaignRecipients_TrackingTokenHash",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "FirstClickedAt",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "FirstOpenedAt",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "OpenCount",
                table: "EmailCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "TrackingTokenHash",
                table: "EmailCampaignRecipients");
        }
    }
}
