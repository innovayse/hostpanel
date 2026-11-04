using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportDedupeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Downloads_CategoryId",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_DownloadCategories_ParentCategoryId",
                table: "DownloadCategories");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkIssues_Title_StartDate",
                table: "NetworkIssues",
                columns: new[] { "Title", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_CategoryId_Title",
                table: "Downloads",
                columns: new[] { "CategoryId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadCategories_ParentCategoryId_Name",
                table: "DownloadCategories",
                columns: new[] { "ParentCategoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Title_CreatedAt",
                table: "Announcements",
                columns: new[] { "Title", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NetworkIssues_Title_StartDate",
                table: "NetworkIssues");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_CategoryId_Title",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_DownloadCategories_ParentCategoryId_Name",
                table: "DownloadCategories");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_Title_CreatedAt",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_CategoryId",
                table: "Downloads",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadCategories_ParentCategoryId",
                table: "DownloadCategories",
                column: "ParentCategoryId");
        }
    }
}
