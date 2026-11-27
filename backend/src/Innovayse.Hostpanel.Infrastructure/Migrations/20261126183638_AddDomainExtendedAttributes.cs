using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainExtendedAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtendedAttributesJson",
                table: "OrderItems",
                type: "character varying(42719)",
                maxLength: 42719,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtendedAttributesJson",
                table: "Domains",
                type: "character varying(42719)",
                maxLength: 42719,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtendedAttributesJson",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ExtendedAttributesJson",
                table: "Domains");
        }
    }
}
