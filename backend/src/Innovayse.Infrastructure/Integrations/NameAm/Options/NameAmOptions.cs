namespace Innovayse.Infrastructure.Integrations.NameAm.Options;

/// <summary>
/// Configuration options for the Name.am registrar API integration.
/// Bound from the "NameAm" section in appsettings.
/// </summary>
/// <remarks>
/// Name.am is optional in the same way Namecheap is: an unset section means the registrar is not
/// in use and the client refuses at the point of use, while a partly filled one is refused at
/// startup via <see cref="IsUsable"/>.
/// </remarks>
public sealed class NameAmOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "NameAm";

    /// <summary>
    /// Name.am account email address used for authentication. Empty means the registrar is not
    /// configured.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Name.am account password used for authentication. A secret, so it carries no default.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Absolute base URL of the Name.am API, scheme included and no trailing slash. The public
    /// endpoint is the same for every account, so unlike the credentials above this one has a
    /// default that is safe to run with.
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.name.am";

    /// <summary>
    /// Whether the Name.am API should operate in test mode. When enabled, a <c>testmode=1</c>
    /// parameter is appended to all API requests and no real domain is registered.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>
    /// Whether the credentials were left unset -- a deployment that does not use Name.am.
    /// <see cref="ApiUrl"/> is excluded because it has a default and is therefore never empty.
    /// </summary>
    public bool IsAbsent => Email.Length == 0 && Password.Length == 0;

    /// <summary>Whether every value a Name.am API call needs is present.</summary>
    public bool IsConfigured => Email.Length > 0 && Password.Length > 0 && ApiUrl.Length > 0;

    /// <summary>
    /// Whether this section is in a state the process may start with -- entirely unset, or
    /// complete. Half-filled is neither.
    /// </summary>
    public bool IsUsable => IsAbsent || IsConfigured;
}
