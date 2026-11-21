using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenSettingValueForLegalTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Settings",
                type: "character varying(64051)",
                maxLength: 64051,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10719)",
                oldMaxLength: 10719);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Settings",
                type: "character varying(10719)",
                maxLength: 10719,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64051)",
                oldMaxLength: 64051);
        }
    }
}
