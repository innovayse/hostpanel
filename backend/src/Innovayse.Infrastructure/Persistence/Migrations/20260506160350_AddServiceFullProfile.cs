using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceFullProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "client_services",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoTerminateEndOfCycle",
                table: "client_services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AutoTerminateReason",
                table: "client_services",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedicatedIp",
                table: "client_services",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "client_services",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPaymentAmount",
                table: "client_services",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "OverrideAutoSuspend",
                table: "client_services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "client_services",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "client_services",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionCode",
                table: "client_services",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "client_services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RecurringAmount",
                table: "client_services",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionId",
                table: "client_services",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendUntil",
                table: "client_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminatedAt",
                table: "client_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "client_services",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "AutoTerminateEndOfCycle",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "AutoTerminateReason",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "DedicatedIp",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "Domain",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "FirstPaymentAmount",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "OverrideAutoSuspend",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "PromotionCode",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "RecurringAmount",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "SuspendUntil",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "client_services");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "client_services");
        }
    }
}
