namespace Innovayse.Domain.Settings;

using System.Text.RegularExpressions;

/// <summary>
/// The <c>portal.*</c> keys in the <c>settings</c> table: what the storefront reads to decide
/// its template, colour mode, branding, identity and contact channels.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place the key list lives on the backend. <c>PortalSettingsSeeder</c> creates
/// the rows from <see cref="Defaults"/> and <c>GetPublicSettingsHandler</c> exposes the rows in
/// <see cref="Public"/> — before this class each held its own hand-maintained copy of the same
/// strings, and a key added to one and not the other failed silently: the row existed, the
/// storefront never saw it, and the site rendered its build-time default as though the operator
/// had never set anything.
/// </para>
/// <para>
/// <see cref="Public"/> is an explicit set and must stay one. A prefix match on <c>portal.</c>
/// would start exposing, without authentication, anything a future contributor happens to name
/// that way — and the same table holds integration credentials under <c>integration:*</c>.
/// </para>
/// <para>
/// The Nuxt BFF keeps a manual twin of this list in
/// <c>client/server/api/portal/public/settings.get.ts</c>; it cannot import this class, so a
/// key added here is added there by hand.
/// </para>
/// </remarks>
public static class PortalSettingKeys
{
    /// <summary>Active storefront template: <c>aurora</c>, <c>nova</c> or <c>classic</c>.</summary>
    public const string Template = "portal.template";

    /// <summary>Header logo for light backgrounds. The original key, kept for existing installs.</summary>
    public const string Logo = "portal.logo";

    /// <summary>Header logo for dark backgrounds. Empty falls back to <see cref="Logo"/>.</summary>
    public const string LogoDark = "portal.logo.dark";

    /// <summary>Square icon-only logo for compact headers and sign-in pages.</summary>
    public const string LogoMark = "portal.logo.mark";

    /// <summary>Browser tab icon; the source of the generated icon set.</summary>
    public const string Favicon = "portal.favicon";

    /// <summary>Colour mode for a visitor with no saved choice: <c>light</c>, <c>dark</c> or <c>system</c>.</summary>
    public const string ThemeDefault = "portal.theme.default";

    /// <summary>Whether visitors see the light/dark switch: <c>true</c> or <c>false</c>.</summary>
    public const string ThemeUserToggle = "portal.theme.user_toggle";

    /// <summary>Site name for the browser title, social previews and image alt text.</summary>
    public const string SiteName = "portal.site.name";

    /// <summary>One-line site description used as the default meta description.</summary>
    public const string SiteTagline = "portal.site.tagline";

    /// <summary>WhatsApp number in international format without the leading <c>+</c>.</summary>
    public const string ContactWhatsapp = "portal.contact.whatsapp";

    /// <summary>Telegram handle without the <c>@</c>.</summary>
    public const string ContactTelegram = "portal.contact.telegram";

    /// <summary>Live chat provider slug; empty disables the widget.</summary>
    public const string ChatProvider = "portal.chat.provider";

    /// <summary>External newsletter form action URL.</summary>
    public const string NewsletterActionUrl = "portal.newsletter.action_url";

    /// <summary>Public support e-mail address shown in the footer.</summary>
    public const string ContactEmail = "portal.contact.email";

    /// <summary>Facebook page URL.</summary>
    public const string SocialFacebook = "portal.social.facebook";

    /// <summary>Instagram profile URL.</summary>
    public const string SocialInstagram = "portal.social.instagram";

    /// <summary>LinkedIn page URL.</summary>
    public const string SocialLinkedin = "portal.social.linkedin";

    /// <summary>YouTube channel URL.</summary>
    public const string SocialYoutube = "portal.social.youtube";

    /// <summary>Public phone number shown in the footer.</summary>
    public const string ContactPhone = "portal.contact.phone";

    /// <summary>Company tax identifier shown in the footer.</summary>
    public const string LegalTaxId = "portal.legal.tax_id";

    /// <summary>Whether the header app launcher is shown: <c>true</c> or <c>false</c>.</summary>
    public const string AppsEnabled = "portal.apps.enabled";

    /// <summary>App launcher URL for the Account entry.</summary>
    public const string AppsAccount = "portal.apps.account";

    /// <summary>App launcher URL for the Tasks entry.</summary>
    public const string AppsTasks = "portal.apps.tasks";

    /// <summary>App launcher URL for the ERP entry.</summary>
    public const string AppsErp = "portal.apps.erp";

    /// <summary>App launcher URL for the Hostpanel entry.</summary>
    public const string AppsHostpanel = "portal.apps.hostpanel";

    /// <summary>App launcher URL for the Sheets entry.</summary>
    public const string AppsSheets = "portal.apps.sheets";

    /// <summary>App launcher URL for the Mail entry.</summary>
    public const string AppsMail = "portal.apps.mail";

    /// <summary>App launcher URL for the Docs entry.</summary>
    public const string AppsDocs = "portal.apps.docs";

    /// <summary>App launcher URL for the Calendar entry.</summary>
    public const string AppsCalendar = "portal.apps.calendar";

    /// <summary>Brand colour as <c>#rrggbb</c>; the storefront derives its whole primary scale from it.</summary>
    public const string BrandPrimary = "portal.brand.primary";

    /// <summary>Accent colour as <c>#rrggbb</c>; the source of the secondary scale.</summary>
    public const string BrandAccent = "portal.brand.accent";

    /// <summary>Heading typeface, one of <see cref="FontNames"/>.</summary>
    public const string BrandFontHeading = "portal.brand.font_heading";

    /// <summary>Body typeface, one of <see cref="FontNames"/>.</summary>
    public const string BrandFontBody = "portal.brand.font_body";

    /// <summary>Google Tag Manager container id (<c>GTM-XXXXXXX</c>). Empty loads no tag.</summary>
    public const string AnalyticsGtmId = "portal.analytics.gtm_id";

    /// <summary>Absolute <c>https://</c> origin of the live-chat server. Empty loads no widget.</summary>
    public const string ChatBaseUrl = "portal.chat.base_url";

    /// <summary>Website token the chat widget identifies this storefront with.</summary>
    public const string ChatWebsiteToken = "portal.chat.website_token";

    /// <summary>Russian-locale chat token; empty falls back to <see cref="ChatWebsiteToken"/>.</summary>
    public const string ChatWebsiteTokenRu = "portal.chat.website_token.ru";

    /// <summary>Armenian-locale chat token; empty falls back to <see cref="ChatWebsiteToken"/>.</summary>
    public const string ChatWebsiteTokenHy = "portal.chat.website_token.hy";

    /// <summary>The template names the storefront can render. Mirrors <c>client/templates/types.ts</c>.</summary>
    public static readonly IReadOnlySet<string> TemplateNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "aurora", "nova", "classic" };

    /// <summary>The colour modes an operator can set as the default.</summary>
    public static readonly IReadOnlySet<string> ThemeModes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "light", "dark", "system" };

    /// <summary>The two spellings a boolean setting accepts.</summary>
    public static readonly IReadOnlySet<string> Booleans =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "true", "false" };

    /// <summary>
    /// The typefaces an operator can pick. Mirrors <c>client/utils/brandFonts.ts</c>, whose
    /// stacks are the self-hosted <c>@fontsource</c> packages the storefront already ships —
    /// nothing here causes a request to a font CDN. <c>system</c> is the device's own face.
    /// </summary>
    public static readonly IReadOnlySet<string> FontNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inter", "noto-sans-armenian", "noto-serif-armenian", "system",
        };

    /// <summary>
    /// A shape a free-text key must match, with the sentence a refusal shows.
    /// </summary>
    /// <param name="Regex">The pattern; the whole trimmed value must match.</param>
    /// <param name="Expected">What a valid value looks like, for the operator.</param>
    /// <param name="Canonical">Turns an accepted value into the spelling that is stored.</param>
    public sealed record ValuePattern(Regex Regex, string Expected, Func<string, string> Canonical);

    // Declared before Patterns: static fields initialise in textual order, and the dictionary
    // below reads this one.
    /// <summary>A six-digit hex colour, stored lower-case with its <c>#</c>.</summary>
    private static readonly ValuePattern HexColour = new(
        new Regex("^#[0-9a-f]{6}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        "a colour like #1a73e8",
        v => v.ToLowerInvariant());

    /// <summary>
    /// The keys whose value is free text but must have a shape, and that shape.
    /// <para>
    /// Checked by <c>UpdateSettingHandler</c> for keys that have no <see cref="AllowedValues"/>
    /// entry. Every one of these seeds empty, so an empty write passes through
    /// <see cref="AllowsEmpty"/> before the shape is ever looked at. The canonical spelling is
    /// stored so the storefront and the e-mail renderer can compare and concatenate without
    /// normalising again.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [BrandPrimary] = HexColour,
            [BrandAccent] = HexColour,
            [AnalyticsGtmId] = new(
                new Regex("^GTM-[A-Z0-9]{4,12}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                "a container id like GTM-ABC1234",
                v => v.ToUpperInvariant()),
            [ChatBaseUrl] = new(
                new Regex("^https://[^\\s/]+(/[^\\s]*)?$", RegexOptions.CultureInvariant),
                "an https:// address like https://chat.example.com",
                v => v.TrimEnd('/')),
        };


    /// <summary>
    /// The keys whose value must come from a fixed vocabulary, and that vocabulary.
    /// <para>
    /// A key absent from this dictionary accepts any string. Comparison is case-insensitive
    /// so <c>Aurora</c> is the same choice as <c>aurora</c>; <c>UpdateSettingHandler</c> stores the
    /// canonical spelling from the set, because the storefront matches the value exactly.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedValues =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Template] = TemplateNames,
            [ThemeDefault] = ThemeModes,
            [ThemeUserToggle] = Booleans,
            [AppsEnabled] = Booleans,
            [BrandFontHeading] = FontNames,
            [BrandFontBody] = FontNames,
        };

    /// <summary>
    /// The keys created on a fresh install, with the values a new install starts from and the
    /// description the admin panel shows beside each.
    /// <para>
    /// Every contact channel and image starts empty, which the storefront renders as "hidden"
    /// or "built-in" rather than as a broken link. <see cref="ThemeDefault"/> starts as
    /// <c>dark</c> and <see cref="ThemeUserToggle"/> as <c>true</c> because that is what the
    /// storefront did before either key existed — an upgrade changes nothing an operator sees.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Value, string Description)> Defaults =
    [
        (Template,            "aurora", "Active storefront template: aurora, nova or classic."),
        (SiteName,            "",       "Site name used in the browser title, social previews and image alt text. Empty shows the built-in name."),
        (SiteTagline,         "",       "One-line description used as the default meta description. Empty hides it."),
        (ThemeDefault,        "dark",   "Colour mode for a visitor who has not chosen one: light, dark or system."),
        (ThemeUserToggle,     "true",   "Show the light/dark switch to visitors: true or false."),
        (Logo,                "",       "Logo shown on light backgrounds. Empty renders the built-in mark and wordmark."),
        (LogoDark,            "",       "Logo shown on dark backgrounds. Empty falls back to the light logo."),
        (LogoMark,            "",       "Square icon-only logo for compact headers and sign-in pages. Empty falls back to the built-in mark."),
        (Favicon,             "",       "URL of the browser tab icon. Empty falls back to the built-in favicon."),
        (BrandPrimary,        "",       "Brand colour as #rrggbb; buttons, links and highlights derive from it. Empty keeps the template's own."),
        (BrandAccent,         "",       "Accent colour as #rrggbb; gradients and secondary highlights. Empty keeps the template's own."),
        (BrandFontHeading,    "",       "Heading typeface: inter, noto-sans-armenian, noto-serif-armenian or system. Empty keeps the template's own."),
        (BrandFontBody,       "",       "Body typeface: inter, noto-sans-armenian, noto-serif-armenian or system. Empty keeps the template's own."),
        (AnalyticsGtmId,      "",       "Google Tag Manager container id (GTM-XXXXXXX). Loads only after a visitor accepts all cookies. Empty loads nothing."),
        (ChatBaseUrl,         "",       "Live chat server origin, https:// only. Empty hides the widget."),
        (ChatWebsiteToken,    "",       "Live chat website token. Empty hides the widget."),
        (ChatWebsiteTokenRu,  "",       "Live chat website token for Russian visitors. Empty uses the default token."),
        (ChatWebsiteTokenHy,  "",       "Live chat website token for Armenian visitors. Empty uses the default token."),
        (ContactWhatsapp,     "",       "WhatsApp number in international format, no leading +. Empty hides the action."),
        (ContactTelegram,     "",       "Telegram handle without the @. Empty hides the action."),
        (ChatProvider,        "",       "Live chat provider: chatwoot, or empty to disable the widget."),
        (NewsletterActionUrl, "",       "External newsletter form action URL. Empty hides the footer block."),
        (ContactEmail,        "",       "Public support address shown in the storefront footer. Empty hides it."),
        (SocialFacebook,      "",       "Facebook page URL. Empty hides the icon."),
        (SocialInstagram,     "",       "Instagram profile URL. Empty hides the icon."),
        (SocialLinkedin,      "",       "LinkedIn page URL. Empty hides the icon."),
        (SocialYoutube,       "",       "YouTube channel URL. Empty hides the icon."),
        (ContactPhone,        "",       "Public phone number shown in the footer. Empty hides it."),
        (LegalTaxId,          "",       "Company tax identifier shown in the footer. Empty hides it."),
        (AppsEnabled,         "false",  "Show the header app launcher. Off unless this deployment runs the apps it links to."),
        (AppsAccount,         "",       "URL for the Account entry in the header app launcher. Empty hides it."),
        (AppsTasks,           "",       "URL for the Tasks entry in the header app launcher. Empty hides it."),
        (AppsErp,             "",       "URL for the ERP entry in the header app launcher. Empty hides it."),
        (AppsHostpanel,       "",       "URL for the Hostpanel entry in the header app launcher. Empty hides it."),
        (AppsSheets,          "",       "URL for the Sheets entry in the header app launcher. Empty hides it."),
        (AppsMail,            "",       "URL for the Mail entry in the header app launcher. Empty hides it."),
        (AppsDocs,            "",       "URL for the Docs entry in the header app launcher. Empty hides it."),
        (AppsCalendar,        "",       "URL for the Calendar entry in the header app launcher. Empty hides it."),
    ];

    /// <summary>
    /// Whether an empty value is a valid write for a key: true exactly when the key seeds
    /// empty, because empty then means "unset, use the built-in default". A key that seeds
    /// with a value (the template, the theme mode, the booleans) never had an empty state and
    /// refuses one.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <returns>True when an empty value may be stored without any vocabulary or shape check.</returns>
    public static bool AllowsEmpty(string key) =>
        Defaults.Any(d => d.Key == key && d.Value.Length == 0);

    /// <summary>
    /// The only keys the anonymous storefront endpoint will ever return.
    /// <para>
    /// Derived from <see cref="Defaults"/> so the two cannot drift: every seeded
    /// <c>portal.*</c> key is public, and nothing that is not seeded here is. That holds
    /// because none of these values is a secret — the whole list is what the storefront
    /// renders into public HTML. A future <c>portal.*</c> key that must <b>not</b> be public
    /// does not belong in <see cref="Defaults"/>; give it its own list.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> Public =
        Defaults.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
}
