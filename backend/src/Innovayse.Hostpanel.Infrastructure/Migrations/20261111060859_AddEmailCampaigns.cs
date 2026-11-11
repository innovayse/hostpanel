using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UnsubscribeTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_EmailCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    FilterType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FilterValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaignSegments_EmailCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_CampaignId_ClientId",
                table: "EmailCampaignRecipients",
                columns: new[] { "CampaignId", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_CampaignId_Status_Id",
                table: "EmailCampaignRecipients",
                columns: new[] { "CampaignId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_ClientId",
                table: "EmailCampaignRecipients",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_UnsubscribeTokenHash",
                table: "EmailCampaignRecipients",
                column: "UnsubscribeTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_Status_ScheduledAt",
                table: "EmailCampaigns",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignSegments_CampaignId",
                table: "EmailCampaignSegments",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailCampaignRecipients");

            migrationBuilder.DropTable(
                name: "EmailCampaignSegments");

            migrationBuilder.DropTable(
                name: "EmailCampaigns");
        }
    }
}
