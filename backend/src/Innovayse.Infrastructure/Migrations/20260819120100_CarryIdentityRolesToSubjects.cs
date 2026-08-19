using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <summary>
    /// Copies the role assignments held in Identity's <c>AspNetUserRoles</c> into
    /// <c>subject_roles</c>, keyed by whatever this deployment calls a person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AddSubjectRoles</c> created the table and left it empty. Authorization reads
    /// from it in both modes, so on any deployment that already had users, the first
    /// start after that migration is one where nobody holds any role: the admin panel
    /// locks its administrator out, and every client loses the role their portal access
    /// depends on. Production is 19 assignments — one Admin and eighteen Clients.
    /// </para>
    /// <para>
    /// Worse than the lockout is what the lockout invites. <c>/api/auth/setup</c> grants
    /// Admin to the first authenticated caller while nobody holds it, precisely so a new
    /// deployment can be claimed. With the table wrongly empty, an established one is
    /// claimable too — by whoever asks first.
    /// </para>
    /// <para>
    /// The subject is <c>SsoSubjectId</c> where the person has one and the local id where
    /// they do not, which is the same rule the rest of this product follows: the subject
    /// is whatever the configured identity provider calls somebody. That makes this
    /// correct on a standalone deployment too, where it keys every row by the local id
    /// and is not a no-op — unlike the client rewrite alongside it, standalone needs this
    /// one just as much.
    /// </para>
    /// <para>
    /// Re-running it changes nothing: the pair is the primary key, and a conflict is
    /// ignored rather than raised.
    /// </para>
    /// </remarks>
    public partial class CarryIdentityRolesToSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO subject_roles ("Subject", "Role")
                SELECT COALESCE(u."SsoSubjectId", u."Id"), r."Name"
                  FROM "AspNetUserRoles" ur
                  JOIN "AspNetUsers" u ON u."Id"  = ur."UserId"
                  JOIN "AspNetRoles"  r ON r."Id" = ur."RoleId"
                 WHERE r."Name" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes what Up inserted, and nothing else — a role granted since, to
            // somebody who never had it in AspNetUserRoles, does not match and stays.
            migrationBuilder.Sql("""
                DELETE FROM subject_roles sr
                 WHERE (sr."Subject", sr."Role") IN (
                       SELECT COALESCE(u."SsoSubjectId", u."Id"), r."Name"
                         FROM "AspNetUserRoles" ur
                         JOIN "AspNetUsers" u ON u."Id"  = ur."UserId"
                         JOIN "AspNetRoles"  r ON r."Id" = ur."RoleId"
                        WHERE r."Name" IS NOT NULL);
                """);
        }
    }
}
