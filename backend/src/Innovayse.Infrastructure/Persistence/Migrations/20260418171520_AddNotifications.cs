#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Innovayse.Infrastructure.Persistence.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    To = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "email_templates",
                columns: new[] { "Id", "Body", "Description", "IsActive", "Slug", "Subject" },
                values: new object[,]
                {
                    { 1, "<h1>Welcome!</h1><p>Hello, your account has been created successfully.</p>", "Sent on client registration", true, "welcome", "Welcome to Innovayse!" },
                    { 2, "<p>Your invoice for {{invoice.total}} is ready.</p>", "Sent when invoice created", true, "invoice-created", "Invoice #{{invoice.id}} Created" },
                    { 3, "<p>Thank you for your payment of {{invoice.total}}.</p>", "Sent on payment", true, "payment-received", "Payment Received" },
                    { 4, "<p>Your hosting service has been provisioned.</p>", "Sent when service provisioned", true, "service-provisioned", "Your Service is Ready" },
                    { 5, "<p>We received your support request.</p>", "Sent when ticket created", true, "ticket-created", "Support Ticket #{{ticket.id}} Created" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_Slug",
                table: "email_templates",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "email_templates");
        }
    }
}
