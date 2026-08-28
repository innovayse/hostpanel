namespace Innovayse.Infrastructure.Integrations.CPanel.Options;

/// <summary>
/// Configuration options for the cPanel WHM API integration.
/// Bound from the "CPanel" section in appsettings.
/// </summary>
/// <remarks>
/// These describe the single WHM server the DI-registered cPanel client talks to. Most deployments
/// provision through per-server credentials held in the database instead and leave this section
/// unset, which is allowed; a partly filled section is refused at startup via
/// <see cref="IsUsable"/>.
/// </remarks>
public sealed class CPanelOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "CPanel";

    /// <summary>
    /// Absolute URL of the WHM API, scheme and port included and no trailing slash -- WHM listens
    /// on 2087 for TLS, e.g. <c>https://server.example.com:2087</c>. Empty means no single-server
    /// cPanel target is configured.
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>The WHM account username the API token belongs to, usually <c>root</c>.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The WHM API token. A secret, so it carries no default.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// The server IPv4 address handed to WHM when creating an account, so the account is bound to
    /// the intended shared or dedicated address.
    /// </summary>
    public string ServerIp { get; set; } = string.Empty;

    /// <summary>
    /// Whether the whole section was left unset -- a deployment with no single-server cPanel
    /// target.
    /// </summary>
    public bool IsAbsent =>
        ApiUrl.Length == 0 && Username.Length == 0 && ApiToken.Length == 0 && ServerIp.Length == 0;

    /// <summary>
    /// Whether the values a WHM API call cannot be made without are present. <see cref="ServerIp"/>
    /// is excluded: it is only sent when creating an account, and WHM picks the server default
    /// when it is omitted.
    /// </summary>
    public bool IsConfigured =>
        ApiUrl.Length > 0 && Username.Length > 0 && ApiToken.Length > 0;

    /// <summary>
    /// Whether this section is in a state the process may start with -- entirely unset, or
    /// complete. Half-filled is neither.
    /// </summary>
    public bool IsUsable => IsAbsent || IsConfigured;
}
