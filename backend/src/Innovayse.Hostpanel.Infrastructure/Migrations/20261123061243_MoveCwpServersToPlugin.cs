using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCwpServersToPlugin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TRANSITIONAL: listed for the post-deploy cleanup; once every deployment has run it, nothing here is needed again.
            // The host no longer carries its own Control Web Panel provider; the plugin with the id "cwp" serves the
            // module "Cwp7". It reads the API key from the same server columns the old provider used and takes
            // its optional settings from their defaults, so a server needs only the right module and the plugin
            // needs its switch on. Servers kept the older module text "Centos" for the same panel.
            migrationBuilder.Sql(
                """
                UPDATE "Servers" SET "Module" = 'Cwp7' WHERE lower("Module") = 'centos';

                INSERT INTO "Settings" ("Key", "Value", "Description")
                SELECT 'integration:cwp:is_enabled', 'true', 'Whether the cwp integration is enabled.'
                WHERE EXISTS (SELECT 1 FROM "Servers" WHERE lower("Module") = 'cwp7')
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: a server that was named "Centos" cannot be told from one that always was "Cwp7",
            // and the switch is the operator's setting from here on.
        }
    }
}
