namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// A person, as whichever system owns people describes them.
///
/// <para>
/// Named for the account rather than the user because this codebase already has an
/// <c>IdentityUser</c> — ASP.NET Identity's, which <c>AppUser</c> derives from. Two types
/// of that name, one of them not a person's record at all, is a trap for the next reader.
/// </para>
/// </summary>
/// <param name="Subject">
/// How the configured provider names this person: an SSO subject, or a local Identity id.
/// The only identifier the domain ever stores.
/// </param>
/// <param name="Email">The address the person signs in with.</param>
/// <param name="FirstName">Given name, empty if the provider does not hold one.</param>
/// <param name="LastName">Family name, empty if the provider does not hold one.</param>
/// <param name="TwoFactorEnabled">
/// Whether a second factor is set up. Read-only here: it is turned on and off wherever the
/// account lives, and this product only shows it.
/// </param>
public sealed record IdentityAccount(
    string Subject,
    string Email,
    string FirstName,
    string LastName,
    bool TwoFactorEnabled = false);

/// <summary>
/// Reads people from wherever they live — this product's own database, or the SSO.
///
/// <para>
/// Read-only on purpose. In SSO mode this product stores no person records at all, so
/// there is no create, no update and no delete to offer; an interface that had them would
/// need half its surface to throw. Registration, passwords and profile edits belong to
/// whoever owns the accounts.
/// </para>
/// </summary>
public interface IIdentityProvider
{
    /// <summary>The person with this subject, or null if the provider does not know them.</summary>
    Task<IdentityAccount?> FindBySubjectAsync(string subject, CancellationToken ct);

    /// <summary>The person with this email address, or null.</summary>
    Task<IdentityAccount?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Email addresses for many subjects at once, keyed by subject. Subjects the provider
    /// does not know are absent rather than null-valued.
    ///
    /// <para>
    /// Exists because the admin screens resolve a page of client rows at a time, and doing
    /// that one lookup per row is a round trip per row — over the SSO's HTTP API, that is
    /// the difference between one request and fifty.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetEmailsBySubjectsAsync(
        IEnumerable<string> subjects, CancellationToken ct);

    /// <summary>One page of people, optionally filtered, with the unpaged total.</summary>
    Task<(IReadOnlyList<IdentityAccount> Items, int Total)> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct);
}
