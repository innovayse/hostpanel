namespace Innovayse.Infrastructure.Email.Mailcow;

/// <summary>
/// Configuration settings for the Mailcow mail server API integration.
/// Bind from <c>appsettings.json</c> section <c>Mailcow</c>.
/// </summary>
public sealed class MailcowSettings
{
    /// <summary>Gets the base URL of the Mailcow API (e.g. "https://mail.innovayse.com").</summary>
    public string ApiUrl { get; init; } = "";

    /// <summary>Gets the Mailcow API key used for authenticating all requests.</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>
    /// Gets the mail hostname (e.g. "mail.innovayse.com").
    /// Used when constructing MX and autodiscover DNS records for provisioned domains.
    /// </summary>
    public string MailHostname { get; init; } = "";
}
