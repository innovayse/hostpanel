using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Adds <c>Servers.ApiHost</c>: the address a control panel's API is dialled at when it does not
    /// answer at the server's hostname, such as a panel whose API port is only reachable on a private
    /// or VPN address. Null keeps the previous behaviour, so existing rows need no backfill.
    /// </summary>
    public partial class AddServerApiHost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiHost",
                table: "Servers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiHost",
                table: "Servers");
        }
    }
}
