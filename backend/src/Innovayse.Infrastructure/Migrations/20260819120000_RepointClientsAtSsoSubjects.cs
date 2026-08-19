using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <summary>
    /// Points client rows at the SSO subject of the person who owns them, where that
    /// person now lives in the SSO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>clients.UserId</c> holds whatever this product's identity provider calls a
    /// person. Standalone, that is the local <c>AspNetUsers.Id</c> and always has been.
    /// Where the SSO owns the people, it is the SSO subject — and once the identity
    /// provider starts answering from the SSO, a row still holding a local id resolves
    /// to nobody. The customer keeps their account and loses their clients, services and
    /// invoices with it.
    /// </para>
    /// <para>
    /// This is the whole rewrite. A scan of every text column in the schema found exactly
    /// one that stores a local user id: this one. <c>client_users</c> has the same shape
    /// but no rows, and the <c>Subject</c> columns on tickets, quotes and email logs are
    /// subject lines, not identities.
    /// </para>
    /// <para>
    /// <b>It is a no-op unless the SSO owns the people.</b> The update only touches rows
    /// whose owner has a subject, and a standalone deployment has given nobody one. That
    /// is what lets the same migration ship to both, rather than living in a runbook that
    /// somebody has to remember to run.
    /// </para>
    /// <para>
    /// Re-running it changes nothing. After the first pass no <c>clients.UserId</c> matches
    /// an <c>AspNetUsers.Id</c> any more, so the join finds nothing to do.
    /// </para>
    /// <para>
    /// Rows pointing at neither a local id nor a subject are left alone. They are already
    /// orphans — remnants of accounts deleted back when deleting removed the row — and
    /// this migration is not the place to decide what becomes of them.
    /// </para>
    /// </remarks>
    public partial class RepointClientsAtSsoSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE clients c
                   SET "UserId" = u."SsoSubjectId"
                  FROM "AspNetUsers" u
                 WHERE u."Id" = c."UserId"
                   AND u."SsoSubjectId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Exactly reversible, because the account row still carries both values: the
            // subject it was rewritten to and the local id it came from. Deleting a user
            // keeps the row now, so the pair survives the reversal.
            migrationBuilder.Sql("""
                UPDATE clients c
                   SET "UserId" = u."Id"
                  FROM "AspNetUsers" u
                 WHERE u."SsoSubjectId" = c."UserId";
                """);
        }
    }
}
