using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerAcceptsSelfSignedCertificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsSelfSignedCertificate",
                table: "Servers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsSelfSignedCertificate",
                table: "Servers");
        }
    }
}
