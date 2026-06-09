using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "migration_jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientsTotal = table.Column<int>(type: "integer", nullable: false),
                    ClientsImported = table.Column<int>(type: "integer", nullable: false),
                    InvoicesTotal = table.Column<int>(type: "integer", nullable: false),
                    InvoicesImported = table.Column<int>(type: "integer", nullable: false),
                    ServicesTotal = table.Column<int>(type: "integer", nullable: false),
                    ServicesImported = table.Column<int>(type: "integer", nullable: false),
                    DomainsTotal = table.Column<int>(type: "integer", nullable: false),
                    DomainsImported = table.Column<int>(type: "integer", nullable: false),
                    TicketsTotal = table.Column<int>(type: "integer", nullable: false),
                    TicketsImported = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_migration_jobs_Key",
                table: "migration_jobs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "migration_jobs");
        }
    }
}
