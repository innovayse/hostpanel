namespace Innovayse.Application.Notifications.Common;

/// <summary>
/// What an outgoing mail needs to look like the storefront: the site's name, its logo as an
/// absolute URL, and the brand colours with the text colour that stays readable on them.
/// </summary>
/// <remarks>
/// Every value is filled in — the operator's setting where there is one, the storefront's own
/// default otherwise — so a template can use <c>{{ brand.primary }}</c> without a guard.
/// Exposed to Liquid as the <c>brand</c> and <c>site</c> objects by <c>TemplateRenderer</c>.
/// </remarks>
/// <param name="SiteName">The site name; <c>portal.site.name</c> or the built-in name.</param>
/// <param name="LogoUrl">Absolute URL of the dark-background logo, or empty when none is uploaded.</param>
/// <param name="Primary">Brand colour as <c>#rrggbb</c>.</param>
/// <param name="PrimaryDark">A darker shade of it for hover and borders.</param>
/// <param name="OnPrimary">Text colour on a surface filled with <see cref="Primary"/>.</param>
/// <param name="Accent">Accent colour as <c>#rrggbb</c>.</param>
public sealed record EmailBranding(
    string SiteName,
    string LogoUrl,
    string Primary,
    string PrimaryDark,
    string OnPrimary,
    string Accent)
{
    /// <summary>The storefront's built-in primary, Tailwind's sky-500, used when no colour is set.</summary>
    public const string DefaultPrimary = "#0ea5e9";

    /// <summary>The storefront's built-in accent, Tailwind's purple-500.</summary>
    public const string DefaultAccent = "#a855f7";

    /// <summary>The built-in site name.</summary>
    public const string DefaultSiteName = "Innovayse";

    /// <summary>The dictionary shape Liquid reads as <c>brand.*</c>.</summary>
    /// <returns>Lower-snake keys, matching the storefront's setting names.</returns>
    public IReadOnlyDictionary<string, string> ToBrandModel() => new Dictionary<string, string>
    {
        ["primary"] = Primary,
        ["primary_dark"] = PrimaryDark,
        ["on_primary"] = OnPrimary,
        ["accent"] = Accent,
        ["logo_url"] = LogoUrl,
    };

    /// <summary>The dictionary shape Liquid reads as <c>site.*</c>.</summary>
    /// <returns>Lower-snake keys.</returns>
    public IReadOnlyDictionary<string, string> ToSiteModel() => new Dictionary<string, string>
    {
        ["name"] = SiteName,
    };
}
