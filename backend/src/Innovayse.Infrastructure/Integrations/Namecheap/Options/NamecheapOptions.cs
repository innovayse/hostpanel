namespace Innovayse.Infrastructure.Integrations.Namecheap.Options;

/// <summary>
/// Configuration options for the Namecheap XML API v2 registrar integration.
/// Bound from the "Namecheap" section in appsettings.
/// </summary>
/// <remarks>
/// Namecheap is one of several registrar back ends and most deployments configure none of it. A
/// wholly unset section is therefore allowed and the client refuses at the point of use; a partly
/// filled one is refused at startup via <see cref="IsUsable"/>.
/// </remarks>
public sealed class NamecheapOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Namecheap";

    /// <summary>
    /// Namecheap API user account username. Empty means the registrar is not configured.
    /// </summary>
    public string ApiUser { get; set; } = string.Empty;

    /// <summary>Namecheap API key for the account. A secret, so it carries no default.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The whitelisted caller IP address the Namecheap API requires on every request, as a bare
    /// IPv4 literal (e.g. <c>203.0.113.10</c>). It must match an address whitelisted in the
    /// Namecheap account or every call is refused.
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// Absolute base URL of the Namecheap XML API endpoint, scheme included -- the live endpoint
    /// is <c>https://api.namecheap.com/xml.response</c> and the sandbox endpoint is a different
    /// host, so this is set alongside <see cref="Sandbox"/> rather than derived from it.
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether the sandbox (test) environment is active. Sandbox calls do not register real
    /// domains and do not spend real money.
    /// </summary>
    public bool Sandbox { get; set; }

    /// <summary>
    /// Whether the whole section was left unset -- a deployment that does not use Namecheap.
    /// </summary>
    public bool IsAbsent =>
        ApiUser.Length == 0 && ApiKey.Length == 0 && ClientIp.Length == 0 && ApiUrl.Length == 0;

    /// <summary>
    /// Whether the credentials a Namecheap API call cannot be made without are present.
    /// <see cref="ClientIp"/> is excluded: the API refuses a request from an unwhitelisted address
    /// with an error of its own, and a deployment behind a single stable egress address may
    /// legitimately leave it to the account's whitelist.
    /// </summary>
    public bool IsConfigured =>
        ApiUser.Length > 0 && ApiKey.Length > 0 && ApiUrl.Length > 0;

    /// <summary>
    /// Whether this section is in a state the process may start with -- entirely unset, or
    /// complete. Half-filled is neither.
    /// </summary>
    public bool IsUsable => IsAbsent || IsConfigured;
}
