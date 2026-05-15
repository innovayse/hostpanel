namespace Innovayse.Infrastructure.Auth;

using Microsoft.AspNetCore.Identity;

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

    /// <summary>Gets or sets the preferred UI language code (en, ru, hy). Null means system default.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last successful login. Null if never logged in.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
