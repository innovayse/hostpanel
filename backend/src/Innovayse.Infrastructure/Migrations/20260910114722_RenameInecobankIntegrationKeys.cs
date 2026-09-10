using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <summary>
    /// Moves the Inecobank integration's saved settings from the old plugin id to the new one.
    /// </summary>
    /// <remarks>
    /// Data only — the schema does not change. The plugin was renamed from
    /// <c>innovayse-inecobank</c> to <c>inecobank</c>, and its configuration lives in the
    /// Settings table under keys built from that id (<c>integration:&lt;id&gt;:&lt;field&gt;</c>).
    /// Renaming the plugin without moving the rows would leave a deployment that had already been
    /// configured looking untouched: the resolver would find nothing under the new id, the gateway
    /// would quietly stop resolving, and the gateway would disappear from checkout while its
    /// credentials sat in the database under a name nothing reads any more.
    /// </remarks>
    public partial class RenameInecobankIntegrationKeys : Migration
    {
        /// <summary>Old plugin id, as stored by deployments configured before the rename.</summary>
        private const string OldPrefix = "integration:innovayse-inecobank:";

        /// <summary>Current plugin id.</summary>
        private const string NewPrefix = "integration:inecobank:";

        /// <summary>Repoints the saved settings at the new plugin id.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
            => Move(migrationBuilder, OldPrefix, NewPrefix);

        /// <summary>Puts the settings back under the old plugin id.</summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
            => Move(migrationBuilder, NewPrefix, OldPrefix);

        /// <summary>
        /// Rewrites the prefix of every Inecobank settings key.
        /// </summary>
        /// <remarks>
        /// The delete runs first so the update cannot collide with the unique index on Key. Rows
        /// under the destination prefix can only exist if someone configured the plugin again
        /// after the rename, and in that case theirs is the current configuration — the stale
        /// copy is what goes.
        /// </remarks>
        /// <param name="migrationBuilder">The migration builder.</param>
        /// <param name="from">Prefix to move away from.</param>
        /// <param name="to">Prefix to move to.</param>
        private static void Move(MigrationBuilder migrationBuilder, string from, string to)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM "settings"
                WHERE "Key" LIKE '{from}%'
                  AND EXISTS (
                      SELECT 1 FROM "settings" existing
                      WHERE existing."Key" = '{to}' || substring("settings"."Key" from {from.Length + 1})
                  );

                UPDATE "settings"
                SET "Key" = '{to}' || substring("Key" from {from.Length + 1})
                WHERE "Key" LIKE '{from}%';
                """);
        }
    }
}
