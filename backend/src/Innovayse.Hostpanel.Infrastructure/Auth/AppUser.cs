using Microsoft.AspNetCore.Identity;

namespace Innovayse.Hostpanel.Infrastructure.Auth;

/// <summary>
/// Application user that extends <see cref="IdentityUser"/> with profile fields.
/// Stored in the <c>AspNetUsers</c> table via EF Core Identity.
/// </summary>
public sealed class AppUser : IdentityUser
{
    /// <summary>Gets or sets the user's first name.</summary>
    public required string FirstName { get; set; }

    /// <summary>Gets or sets the user's last name.</summary>
    public required string LastName { get; set; }

    /// <summary>Gets or sets the preferred UI language code (en, ru, hy, de, es). Null means system default.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last successful login. Null if never logged in.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the account was created. Stamped on the first save.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the Base32-encoded TOTP secret. Null when 2FA is not configured.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>
    /// Gets or sets the Base32-encoded TOTP secret issued by a setup that has not been confirmed yet.
    /// Null when no setup is in progress.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="TwoFactorSecret"/> so starting a setup never replaces the secret
    /// an enabled account signs in with. It becomes the active secret only once a code from the
    /// authenticator app proves the app holds it.
    /// </remarks>
    public string? PendingTwoFactorSecret { get; set; }

    /// <summary>Gets or sets the sign-on provider subject identifier (OpenIddict 'sub' claim). Null for users not yet linked to the sign-on provider.</summary>
    public string? SsoSubjectId { get; set; }

    /// <summary>
    /// Gets or sets when the account was deleted. Null for a live account.
    /// </summary>
    /// <remarks>
    /// Deleting the row outright would take the person's name and address with it while every
    /// client, invoice and ticket still points at their id, leaving blanks nobody could identify.
    /// Keeping the record and marking it lets those screens still say who they belonged to, and
    /// makes the deletion reversible.
    ///
    /// <para>
    /// The sign-on provider settles it the same way, with a <c>DeletedAt</c> of its own. Both stores
    /// answering the same question the same way is what lets one provider interface sit
    /// over them.
    /// </para>
    /// </remarks>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Gets whether this account has been deleted.</summary>
    public bool IsDeleted => DeletedAt is not null;
}
