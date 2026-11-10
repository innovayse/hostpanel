using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The <c>portal.*</c> keys in the <c>Settings</c> table: what the storefront reads to decide
/// its template, colour mode, branding, identity and contact channels.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place the key list lives on the backend. <c>PortalSettingsSeeder</c> creates
/// the rows from <see cref="Defaults"/> and <c>GetPublicSettingsHandler</c> exposes the rows in
/// <see cref="Public"/>. Two hand-maintained copies of the same strings would fail silently
/// when a key is added to one and not the other: the row exists, the storefront never sees it,
/// and the site renders its build-time default as though the operator had never set anything.
/// </para>
/// <para>
/// <see cref="Public"/> is an explicit set and must stay one. A prefix match on <c>portal.</c>
/// would start exposing, without authentication, anything a future contributor happens to name
/// that way, and the same table holds integration credentials under <c>integration:*</c>.
/// </para>
/// <para>
/// The Nuxt BFF keeps a manual twin of this list in
/// <c>client/server/api/portal/public/settings.get.ts</c>; it cannot import this class, so a
/// key added here is added there by hand.
/// </para>
/// </remarks>
public static class PortalSettingKeys
{
    /// <summary>Active storefront template: <c>classic</c>, <c>aurora</c> or <c>nova</c>; see <see cref="StorefrontTemplate"/>.</summary>
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

    /// <summary>Headline client count shown in the hero, e.g. <c>120+</c>.</summary>
    public const string StatsClients = "portal.stats.clients";

    /// <summary>Published uptime figure shown in the hero, e.g. <c>99.9%</c>.</summary>
    public const string StatsUptime = "portal.stats.uptime";

    /// <summary>Where the servers are, shown in the hero, e.g. <c>Armenia, Europe</c>.</summary>
    public const string StatsRegions = "portal.stats.regions";

    /// <summary>Short claim shown in the hero badge.</summary>
    public const string HeroBadge = "portal.hero.badge";

    /// <summary>Street address of the office, shown on the contact page and in the footer.</summary>
    public const string ContactAddress = "portal.contact.address";

    /// <summary>Absolute <c>https://</c> address of the embeddable map of the office.</summary>
    public const string ContactMapUrl = "portal.contact.map_url";

    /// <summary>Twitter profile URL.</summary>
    public const string SocialTwitter = "portal.social.twitter";

    /// <summary>Support working hours shown on the contact page, e.g. <c>Mon-Fri 09:00-18:00</c>.</summary>
    public const string ContactHours = "portal.contact.hours";

    /// <summary>Time zone the working hours are stated in, e.g. <c>GMT+4</c>; shown only beside the hours.</summary>
    public const string ContactTimezone = "portal.contact.timezone";

    /// <summary>Whether the operator states that backups are offered: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesBackups = "portal.features.backups";

    /// <summary>Whether the operator states the storage claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesStorage = "portal.features.storage";

    /// <summary>Whether the operator states the caching claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesCaching = "portal.features.caching";

    /// <summary>Whether the operator states the CDN claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesCdn = "portal.features.cdn";

    /// <summary>Whether the operator states the firewall claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesFirewall = "portal.features.firewall";

    /// <summary>Whether the operator states the malware-scanning claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesMalware = "portal.features.malware";

    /// <summary>Whether the free-migration badge is shown: <c>true</c> or <c>false</c>.</summary>
    public const string MigrationFree = "portal.migration.free";

    /// <summary>The company story shown on the About page, in English and for any language without its own text.</summary>
    public const string AboutText = "portal.about.text";

    /// <summary>The company story for Russian visitors.</summary>
    public const string AboutTextRu = "portal.about.text.ru";

    /// <summary>The company story for Armenian visitors.</summary>
    public const string AboutTextHy = "portal.about.text.hy";

    /// <summary>Whether the hero stats strip and trust bar section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionStats = "portal.sections.stats";

    /// <summary>Whether the partners strip section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionPartners = "portal.sections.partners";

    /// <summary>Whether the why-choose-us and services blocks section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionServices = "portal.sections.services";

    /// <summary>Whether the how-it-works process section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionProcess = "portal.sections.process";

    /// <summary>Whether the plan cards, products and pricing section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionPlans = "portal.sections.plans";

    /// <summary>Whether the comparison table section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionComparison = "portal.sections.comparison";

    /// <summary>Whether the dashboard preview section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionDashboard = "portal.sections.dashboard";

    /// <summary>Whether the use cases section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionUseCases = "portal.sections.use_cases";

    /// <summary>Whether the migration block section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionMigration = "portal.sections.migration";

    /// <summary>Whether the portfolio projects section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionProjects = "portal.sections.projects";

    /// <summary>Whether the testimonials section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionTestimonials = "portal.sections.testimonials";

    /// <summary>Whether the frequently asked questions section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionFaq = "portal.sections.faq";

    /// <summary>Whether the closing call to action section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionCta = "portal.sections.cta";

    /// <summary>Whether the server locations section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionLocations = "portal.sections.locations";

    /// <summary>Whether the performance figures section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionPerformance = "portal.sections.performance";

    /// <summary>Whether the security features section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionSecurity = "portal.sections.security";

    /// <summary>Whether the one-click application installer section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionApps = "portal.sections.apps";

    /// <summary>Whether the guarantees section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionGuarantees = "portal.sections.guarantees";

    /// <summary>Whether the support channels section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionSupport = "portal.sections.support";

    /// <summary>Whether the trusted-by logo strip section is shown on the home page: <c>true</c> or <c>false</c>.</summary>
    public const string SectionTrusted = "portal.sections.trusted";

    /// <summary>Average server response time shown in the performance section, e.g. <c>180 ms</c>.</summary>
    public const string StatsResponseTime = "portal.stats.response_time";

    /// <summary>Number of sites hosted, shown in the performance section, e.g. <c>12,000+</c>.</summary>
    public const string StatsSitesHosted = "portal.stats.sites_hosted";

    /// <summary>Typical first support response time shown in the performance section, e.g. <c>15 min</c>.</summary>
    public const string StatsSupportResponse = "portal.stats.support_response";

    /// <summary>Whether the operator states the free-SSL claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesSsl = "portal.features.ssl";

    /// <summary>Whether the operator states the DDoS-protection claim: <c>true</c> or <c>false</c>.</summary>
    public const string FeaturesDdos = "portal.features.ddos";

    /// <summary>Number of applications the one-click installer offers, e.g. <c>150+</c>.</summary>
    public const string InstallerAppCount = "portal.installer.app_count";

    /// <summary>Length of the money-back guarantee in days, digits only, e.g. <c>30</c>.</summary>
    public const string GuaranteeMoneyBackDays = "portal.guarantee.money_back_days";

    /// <summary>Whether the operator states the no-hidden-fees guarantee: <c>true</c> or <c>false</c>.</summary>
    public const string GuaranteeNoHiddenFees = "portal.guarantee.no_hidden_fees";

    /// <summary>Typical live-chat response time shown on the chat support card, e.g. <c>2 min</c>.</summary>
    public const string SupportResponseChat = "portal.support.response.chat";

    /// <summary>Typical ticket response time shown on the ticket support card, e.g. <c>1 hour</c>.</summary>
    public const string SupportResponseTicket = "portal.support.response.ticket";

    /// <summary>Typical e-mail response time shown on the e-mail support card, e.g. <c>4 hours</c>.</summary>
    public const string SupportResponseEmail = "portal.support.response.email";

    /// <summary>Typical phone response time shown on the phone support card, e.g. <c>immediate</c>.</summary>
    public const string SupportResponsePhone = "portal.support.response.phone";

    /// <summary>Office address for Russian visitors. Empty uses <see cref="ContactAddress"/>.</summary>
    public const string ContactAddressRu = "portal.contact.address.ru";

    /// <summary>Office address for Armenian visitors. Empty uses <see cref="ContactAddress"/>.</summary>
    public const string ContactAddressHy = "portal.contact.address.hy";

    /// <summary>Support working hours for Russian visitors. Empty uses <see cref="ContactHours"/>.</summary>
    public const string ContactHoursRu = "portal.contact.hours.ru";

    /// <summary>Support working hours for Armenian visitors. Empty uses <see cref="ContactHours"/>.</summary>
    public const string ContactHoursHy = "portal.contact.hours.hy";

    /// <summary>The template a fresh install starts with. Mirrors <c>DEFAULT_TEMPLATE</c> in <c>client/templates/types.ts</c>.</summary>
    public const StorefrontTemplate DefaultTemplate = StorefrontTemplate.Classic;

    /// <summary>The template names the storefront can render, derived from <see cref="StorefrontTemplate"/>.</summary>
    public static readonly IReadOnlySet<string> TemplateNames =
        new HashSet<string>(Enum.GetValues<StorefrontTemplate>().Select(TemplateValue), StringComparer.OrdinalIgnoreCase);

    /// <summary>The value <c>portal.template</c> stores for a template: its member name in lower case.</summary>
    /// <param name="template">The storefront template to store.</param>
    /// <returns>The stored spelling, e.g. <c>classic</c>.</returns>
    public static string TemplateValue(StorefrontTemplate template) => template.ToString().ToLowerInvariant();

    /// <summary>The colour modes an operator can set as the default.</summary>
    public static readonly IReadOnlySet<string> ThemeModes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "light", "dark", "system" };

    /// <summary>The two spellings a boolean setting accepts.</summary>
    public static readonly IReadOnlySet<string> Booleans =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "true", "false" };

    /// <summary>
    /// The typefaces an operator can pick. Mirrors <c>client/utils/brandFonts.ts</c>, whose
    /// stacks are the self-hosted <c>@fontsource</c> packages the storefront already ships:
    /// nothing here causes a request to a font CDN. <c>system</c> is the device's own face.
    /// </summary>
    public static readonly IReadOnlySet<string> FontNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inter", "noto-sans-armenian", "noto-serif-armenian", "system",
        };

    /// <summary>
    /// The keys that hold an uploaded branding image. When one is saved with a new value, the files
    /// its previous value pointed at are removed: nothing references them any more.
    /// </summary>
    public static readonly IReadOnlySet<string> BrandingImages =
        new HashSet<string>(StringComparer.Ordinal) { Logo, LogoDark, LogoMark, Favicon };

    // Declared before Patterns: static fields initialise in textual order, and the dictionary
    // below reads this one.

    /// <summary>A six-digit hex colour, stored lower-case with its <c>#</c>.</summary>
    private static readonly ValuePattern HexColour = new(
        new Regex("^#[0-9a-f]{6}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        "a colour like #1a73e8",
        v => v.ToLowerInvariant());

    /// <summary>An absolute <c>https://</c> address: a host, then an optional path, no spaces.</summary>
    private static readonly Regex HttpsAddress =
        new("^https://[^\\s/]+(/[^\\s]*)?$", RegexOptions.CultureInvariant);

    /// <summary>
    /// A link the storefront renders as an anchor (a social profile, an app launcher entry). Only
    /// <c>https://</c>: a <c>javascript:</c> or plain <c>http:</c> value would otherwise reach every
    /// visitor's page.
    /// </summary>
    private static readonly ValuePattern LinkAddress = new(
        HttpsAddress,
        "an https:// address like https://example.com/page",
        v => v);

    /// <summary>
    /// An image the storefront loads (a logo): an <c>https://</c> address, or a path under
    /// <c>/uploads/</c> as the branding upload stores it.
    /// </summary>
    private static readonly ValuePattern ImageAddress = new(
        new Regex("^(https://[^\\s/]+(/[^\\s]*)?|/uploads/[^\\s]+)$", RegexOptions.CultureInvariant),
        "an https:// address like https://cdn.example.com/logo.svg, or an uploaded path like /uploads/branding/logo.png",
        v => v);

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
                HttpsAddress,
                "an https:// address like https://chat.example.com",
                v => v.TrimEnd('/')),
            [ContactEmail] = new(
                new Regex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.CultureInvariant),
                "an address like support@example.com",
                v => v),
            [ContactMapUrl] = new(
                HttpsAddress,
                "an https:// address like https://maps.example.com/embed",
                v => v),
            [Logo] = ImageAddress,
            [LogoDark] = ImageAddress,
            [LogoMark] = ImageAddress,
            [SocialFacebook] = LinkAddress,
            [SocialInstagram] = LinkAddress,
            [SocialLinkedin] = LinkAddress,
            [SocialYoutube] = LinkAddress,
            [SocialTwitter] = LinkAddress,
            [AppsAccount] = LinkAddress,
            [AppsTasks] = LinkAddress,
            [AppsErp] = LinkAddress,
            [AppsHostpanel] = LinkAddress,
            [AppsSheets] = LinkAddress,
            [AppsMail] = LinkAddress,
            [AppsDocs] = LinkAddress,
            [AppsCalendar] = LinkAddress,
            [GuaranteeMoneyBackDays] = new(
                new Regex("^[0-9]{1,3}$", RegexOptions.CultureInvariant),
                "a whole number of days like 30",
                v => v),
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
            [FeaturesBackups] = Booleans,
            [FeaturesStorage] = Booleans,
            [FeaturesCaching] = Booleans,
            [FeaturesCdn] = Booleans,
            [FeaturesFirewall] = Booleans,
            [FeaturesMalware] = Booleans,
            [MigrationFree] = Booleans,
            [SectionStats] = Booleans,
            [SectionPartners] = Booleans,
            [SectionServices] = Booleans,
            [SectionProcess] = Booleans,
            [SectionPlans] = Booleans,
            [SectionComparison] = Booleans,
            [SectionDashboard] = Booleans,
            [SectionUseCases] = Booleans,
            [SectionMigration] = Booleans,
            [SectionProjects] = Booleans,
            [SectionTestimonials] = Booleans,
            [SectionFaq] = Booleans,
            [SectionCta] = Booleans,
            [SectionLocations] = Booleans,
            [SectionPerformance] = Booleans,
            [SectionSecurity] = Booleans,
            [SectionApps] = Booleans,
            [SectionGuarantees] = Booleans,
            [SectionSupport] = Booleans,
            [SectionTrusted] = Booleans,
            [FeaturesSsl] = Booleans,
            [FeaturesDdos] = Booleans,
            [GuaranteeNoHiddenFees] = Booleans,
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
    /// storefront did before either key existed: an upgrade changes nothing an operator sees.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(Template,            TemplateValue(DefaultTemplate), "Active storefront template: classic, aurora or nova."),
        new(SiteName,            "",       "Site name used in the browser title, social previews and image alt text. Empty shows the built-in name."),
        new(SiteTagline,         "",       "One-line description used as the default meta description. Empty hides it."),
        new(ThemeDefault,        "dark",   "Colour mode for a visitor who has not chosen one: light, dark or system."),
        new(ThemeUserToggle,     "true",   "Show the light/dark switch to visitors: true or false."),
        new(Logo,                "",       "Logo shown on light backgrounds. Empty renders the built-in mark and wordmark."),
        new(LogoDark,            "",       "Logo shown on dark backgrounds. Empty falls back to the light logo."),
        new(LogoMark,            "",       "Square icon-only logo for compact headers and sign-in pages. Empty falls back to the built-in mark."),
        new(Favicon,             "",       "URL of the browser tab icon. Empty falls back to the built-in favicon."),
        new(BrandPrimary,        "",       "Brand colour as #rrggbb; buttons, links and highlights derive from it. Empty keeps the template's own."),
        new(BrandAccent,         "",       "Accent colour as #rrggbb; gradients and secondary highlights. Empty keeps the template's own."),
        new(BrandFontHeading,    "",       "Heading typeface: inter, noto-sans-armenian, noto-serif-armenian or system. Empty keeps the template's own."),
        new(BrandFontBody,       "",       "Body typeface: inter, noto-sans-armenian, noto-serif-armenian or system. Empty keeps the template's own."),
        new(AnalyticsGtmId,      "",       "Google Tag Manager container id (GTM-XXXXXXX). Loads only after a visitor accepts all cookies. Empty loads nothing."),
        new(ChatBaseUrl,         "",       "Live chat server origin, https:// only. Empty hides the widget."),
        new(ChatWebsiteToken,    "",       "Live chat website token. Empty hides the widget."),
        new(ChatWebsiteTokenRu,  "",       "Live chat website token for Russian visitors. Empty uses the default token."),
        new(ChatWebsiteTokenHy,  "",       "Live chat website token for Armenian visitors. Empty uses the default token."),
        new(ContactWhatsapp,     "",       "WhatsApp number in international format, no leading +. Empty hides the action."),
        new(ContactTelegram,     "",       "Telegram handle without the @. Empty hides the action."),
        new(ChatProvider,        "",       "Live chat provider: chatwoot, or empty to disable the widget."),
        new(NewsletterActionUrl, "",       "External newsletter form action URL. Empty hides the footer block."),
        new(ContactEmail,        "",       "Public support address shown in the storefront footer. Empty hides it."),
        new(SocialFacebook,      "",       "Facebook page URL. Empty hides the icon."),
        new(SocialInstagram,     "",       "Instagram profile URL. Empty hides the icon."),
        new(SocialLinkedin,      "",       "LinkedIn page URL. Empty hides the icon."),
        new(SocialYoutube,       "",       "YouTube channel URL. Empty hides the icon."),
        new(ContactPhone,        "",       "Public phone number shown in the footer. Empty hides it."),
        new(LegalTaxId,          "",       "Company tax identifier shown in the footer. Empty hides it."),
        new(AppsEnabled,         "false",  "Show the header app launcher. Off unless this deployment runs the apps it links to."),
        new(AppsAccount,         "",       "URL for the Account entry in the header app launcher. Empty hides it."),
        new(AppsTasks,           "",       "URL for the Tasks entry in the header app launcher. Empty hides it."),
        new(AppsErp,             "",       "URL for the ERP entry in the header app launcher. Empty hides it."),
        new(AppsHostpanel,       "",       "URL for the Hostpanel entry in the header app launcher. Empty hides it."),
        new(AppsSheets,          "",       "URL for the Sheets entry in the header app launcher. Empty hides it."),
        new(AppsMail,            "",       "URL for the Mail entry in the header app launcher. Empty hides it."),
        new(AppsDocs,            "",       "URL for the Docs entry in the header app launcher. Empty hides it."),
        new(AppsCalendar,        "",       "URL for the Calendar entry in the header app launcher. Empty hides it."),
        new(StatsClients,        "",       "Headline client count shown in the hero, e.g. 120+. Empty hides it."),
        new(StatsUptime,         "",       "Published uptime figure shown in the hero, e.g. 99.9%. Empty hides it."),
        new(StatsRegions,        "",       "Server regions shown in the hero, e.g. Armenia, Europe. Empty hides it."),
        new(HeroBadge,           "",       "Short claim shown in the hero badge. Empty shows the built-in text."),
        new(ContactAddress,      "",       "Office street address shown on the contact page and in the footer. Empty hides it."),
        new(ContactMapUrl,       "",       "Embeddable map address of the office, https:// only. Empty hides the map."),
        new(SocialTwitter,       "",       "Twitter profile URL. Empty hides the icon."),
        new(ContactHours,        "",       "Support working hours shown on the contact page. Empty hides the row."),
        new(ContactTimezone,     "",       "Time zone the working hours are stated in, shown beside them. Ignored while the hours are empty."),
        new(ContactAddressRu,    "",       "Office address for Russian visitors. Empty uses the default address."),
        new(ContactAddressHy,    "",       "Office address for Armenian visitors. Empty uses the default address."),
        new(ContactHoursRu,      "",       "Support working hours for Russian visitors. Empty uses the default hours."),
        new(ContactHoursHy,      "",       "Support working hours for Armenian visitors. Empty uses the default hours."),
        new(FeaturesBackups,     "",       "Show the backups claims on the nova template: true or false. Empty hides them."),
        new(FeaturesStorage,     "",       "Show the storage claim on the nova template: true or false. Empty hides it."),
        new(FeaturesCaching,     "",       "Show the caching claim on the nova template: true or false. Empty hides it."),
        new(FeaturesCdn,         "",       "Show the CDN claim on the nova template: true or false. Empty hides it."),
        new(FeaturesFirewall,    "",       "Show the firewall claim on the nova template: true or false. Empty hides it."),
        new(FeaturesMalware,     "",       "Show the malware-scanning claim on the nova template: true or false. Empty hides it."),
        new(MigrationFree,       "",       "Show the free-migration badge on the nova template: true or false. Empty hides it."),
        new(AboutText,           "",       "Company story on the About page; a blank line starts a new paragraph. Empty hides the page."),
        new(AboutTextRu,         "",       "Company story for Russian visitors. Empty uses the default text."),
        new(AboutTextHy,         "",       "Company story for Armenian visitors. Empty uses the default text."),
        new(SectionStats, "true", "Show the hero stats strip and trust bar section on the home page: true or false."),
        new(SectionPartners, "true", "Show the partners strip section on the home page: true or false."),
        new(SectionServices, "true", "Show the why-choose-us and services blocks section on the home page: true or false."),
        new(SectionProcess, "true", "Show the how-it-works process section on the home page: true or false."),
        new(SectionPlans, "true", "Show the plan cards, products and pricing section on the home page: true or false."),
        new(SectionComparison, "true", "Show the comparison table section on the home page: true or false."),
        new(SectionDashboard, "true", "Show the dashboard preview section on the home page: true or false."),
        new(SectionUseCases, "true", "Show the use cases section on the home page: true or false."),
        new(SectionMigration, "true", "Show the migration block section on the home page: true or false."),
        new(SectionProjects, "false", "Show the portfolio projects section on the home page: true or false."),
        new(SectionTestimonials, "true", "Show the testimonials section on the home page: true or false."),
        new(SectionFaq, "true", "Show the frequently asked questions section on the home page: true or false."),
        new(SectionCta, "true", "Show the closing call to action section on the home page: true or false."),
        new(SectionLocations, "true", "Show the server locations section on the home page: true or false."),
        new(SectionPerformance, "true", "Show the performance figures section on the home page: true or false."),
        new(SectionSecurity, "true", "Show the security features section on the home page: true or false."),
        new(SectionApps, "true", "Show the one-click application installer section on the home page: true or false."),
        new(SectionGuarantees, "true", "Show the guarantees section on the home page: true or false."),
        new(SectionSupport, "true", "Show the support channels section on the home page: true or false."),
        new(SectionTrusted, "true", "Show the trusted-by logo strip section on the home page: true or false."),
        new(StatsResponseTime, "", "Average server response time shown in the performance section, e.g. 180 ms. Empty hides it."),
        new(StatsSitesHosted, "", "Number of sites hosted, shown in the performance section, e.g. 12,000+. Empty hides it."),
        new(StatsSupportResponse, "", "Typical first support response time shown in the performance section, e.g. 15 min. Empty hides it."),
        new(FeaturesSsl, "", "Show the SSL card in the security section: true or false. Empty hides it."),
        new(FeaturesDdos, "", "Show the DDoS protection card in the security section: true or false. Empty hides it."),
        new(InstallerAppCount, "", "Number of applications the one-click installer offers, e.g. 150+. Empty hides the count line."),
        new(GuaranteeMoneyBackDays, "", "Money-back guarantee length in days, digits only, e.g. 30. Empty hides the guarantee."),
        new(GuaranteeNoHiddenFees, "", "Show the no-hidden-fees guarantee: true or false. Empty hides it."),
        new(SupportResponseChat, "", "Typical live-chat response time, e.g. 2 min. Empty hides it."),
        new(SupportResponseTicket, "", "Typical ticket response time, e.g. 1 hour. Empty hides it."),
        new(SupportResponseEmail, "", "Typical e-mail response time, e.g. 4 hours. Empty hides it."),
        new(SupportResponsePhone, "", "Typical phone response time, e.g. immediate. Empty hides it."),
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
    /// because none of these values is a secret: the whole list is what the storefront
    /// renders into public HTML. A future <c>portal.*</c> key that must <b>not</b> be public
    /// does not belong in <see cref="Defaults"/>; give it its own list.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> Public =
        Defaults.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
}
