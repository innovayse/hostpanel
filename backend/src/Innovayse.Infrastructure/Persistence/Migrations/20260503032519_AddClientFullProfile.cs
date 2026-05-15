using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientFullProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address2",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSso",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingContact",
                table: "clients",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "clients",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DisableCcProcessing",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LateFees",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingOptIn",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyAffiliate",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDomain",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyGeneral",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyInvoice",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyProduct",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySupport",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OverdueNotices",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeparateInvoices",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StatusUpdate",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TaxExempt",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address2",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "AllowSso",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "BillingContact",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "DisableCcProcessing",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "LateFees",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "MarketingOptIn",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifyAffiliate",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifyDomain",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifyGeneral",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifyInvoice",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifyProduct",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "NotifySupport",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "OverdueNotices",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "SeparateInvoices",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "StatusUpdate",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "TaxExempt",
                table: "clients");
        }
    }
}
