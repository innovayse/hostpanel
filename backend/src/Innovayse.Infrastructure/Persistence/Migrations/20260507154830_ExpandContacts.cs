using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "contacts");

            migrationBuilder.AddColumn<string>(
                name: "Address2",
                table: "contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "contacts",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "NotifyAffiliate",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDomain",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyGeneral",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyInvoice",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyProduct",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySupport",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PostCode",
                table: "contacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address2",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "City",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifyAffiliate",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifyDomain",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifyGeneral",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifyInvoice",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifyProduct",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "NotifySupport",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "PostCode",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "State",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "contacts");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
