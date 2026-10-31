using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Hostpanel.Infrastructure.Migrations
{
    /// <summary>
    /// Grants the Client role to every account already linked to a client (as owner or as a user)
    /// that does not hold it. Staff linked accounts to clients through paths that did not grant the
    /// role, and the portal answers 403 to an account without it. Staff accounts (Admin, Reseller)
    /// are left alone, as the link paths themselves refuse them. Nothing is removed, and a second
    /// run adds nothing.
    /// </summary>
    public partial class GrantClientRoleToLinkedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "SubjectRoles" ("Subject", "Role")
                SELECT DISTINCT linked."UserId", 'Client'
                FROM (
                    SELECT "UserId" FROM "Clients"
                    UNION
                    SELECT "UserId" FROM "ClientUsers"
                ) AS linked
                WHERE linked."UserId" <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "SubjectRoles" held
                      WHERE held."Subject" = linked."UserId"
                        AND held."Role" IN ('Client', 'Admin', 'Reseller'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversed: the grants cannot be told apart from those made by sign-up, orders or
            // invitations, and removing them would lock those clients out of the portal again.
        }
    }
}
