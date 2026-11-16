namespace Innovayse.Hostpanel.Infrastructure.Vat.Settings;

/// <summary>
/// The credentials of the application registered with the UK tax authority's developer hub, which its VAT number check requires.
/// Bound from the <c>Vat:Hmrc</c> section in appsettings.
/// </summary>
/// <remarks>
/// Optional: a deployment that serves no UK clients sets none of it. A UK number is then refused as not configurable
/// (<c>VAT_CHECK_NOT_CONFIGURED</c>) and the daily re-check leaves UK clients out, so the client's status stays as it was.
/// EU numbers are checked without any credentials.
/// </remarks>
public sealed class HmrcVatOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Vat:Hmrc";

    /// <summary>
    /// The API root, absolute and ending in a slash so a relative path extends it. Overridable for the authority's test
    /// environment or a test double.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.service.hmrc.gov.uk/";

    /// <summary>The application's client id. Empty means no UK check is configured.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The application's client secret. A secret, so it carries no default. Empty means no UK check is configured.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Gets whether both credentials are present, so the UK check can be asked.</summary>
    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0;

    /// <summary>
    /// Gets whether the section is in a state the process may start with: unset, or complete. A client id with no secret asks
    /// nothing while looking configured, so it is refused at startup.
    /// </summary>
    public bool IsUsable => (ClientId.Length == 0 && ClientSecret.Length == 0) || IsConfigured;
}
