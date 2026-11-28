using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FulfilmentRecordLastErrorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "FulfilmentRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // The stored English sentences become the codes that are shown in the reader's language: the two
            // sentences the host wrote itself map to their own code, any other text was a plugin's own error.
            migrationBuilder.Sql(
                @"UPDATE ""FulfilmentRecords"" SET ""LastErrorCode"" = CASE
                    WHEN ""LastError"" IS NULL THEN NULL
                    WHEN ""LastError"" = 'The attempt was abandoned before it reported an outcome.' THEN 'FULFILMENT_ATTEMPT_ABANDONED'
                    WHEN ""LastError"" LIKE 'The plugin did not answer within % seconds.' THEN 'FULFILMENT_PLUGIN_TIMED_OUT'
                    ELSE 'FULFILMENT_PLUGIN_FAILED' END");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "FulfilmentRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "FulfilmentRecords",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE ""FulfilmentRecords"" SET ""LastError"" = CASE
                    WHEN ""LastErrorCode"" IS NULL THEN NULL
                    WHEN ""LastErrorCode"" = 'FULFILMENT_ATTEMPT_ABANDONED' THEN 'The attempt was abandoned before it reported an outcome.'
                    WHEN ""LastErrorCode"" = 'FULFILMENT_PLUGIN_TIMED_OUT' THEN 'The plugin did not answer in time.'
                    ELSE 'The plugin reported an error.' END");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "FulfilmentRecords");
        }
    }
}
