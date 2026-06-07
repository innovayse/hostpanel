using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedSiteTouchestateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeployBranch",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployRepoUrl",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployScript",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TouchEstatePublicKey",
                table: "client_services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TouchEstateSecretKey",
                table: "client_services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeployBranch",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DeployRepoUrl",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DeployScript",
                table: "products");

            migrationBuilder.DropColumn(
                name: "TouchEstatePublicKey",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "TouchEstateSecretKey",
                table: "client_services");
        }
    }
}
