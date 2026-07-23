using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDomainDnsVerificationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastDnsVerificationJson",
                table: "email_domains",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDnsVerificationJson",
                table: "email_domains");
        }
    }
}
