#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddServerFullFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessHash",
                table: "servers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedIpAddresses",
                table: "servers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Datacenter",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "servers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyCost",
                table: "servers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Ns1Hostname",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns1Ip",
                table: "servers",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns2Hostname",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns2Ip",
                table: "servers",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns3Hostname",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns3Ip",
                table: "servers",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns4Hostname",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns4Ip",
                table: "servers",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns5Hostname",
                table: "servers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ns5Ip",
                table: "servers",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerStatusAddress",
                table: "servers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseSSL",
                table: "servers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessHash",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "AssignedIpAddresses",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Datacenter",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "MonthlyCost",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns1Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns1Ip",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns2Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns2Ip",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns3Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns3Ip",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns4Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns4Ip",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns5Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "Ns5Ip",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ServerStatusAddress",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "UseSSL",
                table: "servers");
        }
    }
}
