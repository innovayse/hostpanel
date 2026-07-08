using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    DomainName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HostpanelDomainId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MailcowRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DkimSelector = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DkimPublicKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MaxMailboxes = table.Column<int>(type: "integer", nullable: false),
                    MaxAliases = table.Column<int>(type: "integer", nullable: false),
                    MaxQuotaMb = table.Column<int>(type: "integer", nullable: false),
                    DnsVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_aliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailDomainId = table.Column<int>(type: "integer", nullable: false),
                    SourceAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DestinationAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_aliases_email_domains_EmailDomainId",
                        column: x => x.EmailDomainId,
                        principalTable: "email_domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_mailboxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailDomainId = table.Column<int>(type: "integer", nullable: false),
                    LocalPart = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    QuotaMb = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_mailboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_mailboxes_email_domains_EmailDomainId",
                        column: x => x.EmailDomainId,
                        principalTable: "email_domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_aliases_EmailDomainId",
                table: "email_aliases",
                column: "EmailDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_email_domains_ClientId",
                table: "email_domains",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_email_domains_DomainName",
                table: "email_domains",
                column: "DomainName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_mailboxes_EmailDomainId_LocalPart",
                table: "email_mailboxes",
                columns: new[] { "EmailDomainId", "LocalPart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_aliases");

            migrationBuilder.DropTable(
                name: "email_mailboxes");

            migrationBuilder.DropTable(
                name: "email_domains");
        }
    }
}
