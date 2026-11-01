
/** Represents a system setting key-value pair. */
export interface Setting {
  /** Unique setting identifier. */
  id: number
  /** Setting key. */
  key: string
  /** Setting value; always null for a secret, whose value the API never sends. */
  value: string | null
  /** Optional human-readable description. */
  description?: string
  /** Whether the setting holds a secret; it is then write only. */
  isSecret: boolean
  /** Whether a non-blank value is stored. */
  hasValue: boolean
}

/** Keys of the storefront settings the admin cards edit; the strings are the rows' `key` values. */
export const SettingKey = {
  /** The name the storefront calls itself. */
  SiteName: 'portal.site.name',
  /** One-line tagline, the default page description. */
  SiteTagline: 'portal.site.tagline',
  /** Clients figure in the hero stats. */
  StatsClients: 'portal.stats.clients',
  /** Uptime figure in the hero stats. */
  StatsUptime: 'portal.stats.uptime',
  /** Regions figure in the hero stats. */
  StatsRegions: 'portal.stats.regions',
  /** Short badge line above the hero heading. */
  HeroBadge: 'portal.hero.badge',
  /** Contact email. */
  ContactEmail: 'portal.contact.email',
  /** Contact phone. */
  ContactPhone: 'portal.contact.phone',
  /** WhatsApp number. */
  ContactWhatsapp: 'portal.contact.whatsapp',
  /** Telegram handle. */
  ContactTelegram: 'portal.contact.telegram',
  /** Postal address. */
  ContactAddress: 'portal.contact.address',
  /** Link to the address on a map. */
  ContactMapUrl: 'portal.contact.map_url',
  /** Support working hours. */
  ContactHours: 'portal.contact.hours',
  /** Time zone the working hours are stated in. */
  ContactTimezone: 'portal.contact.timezone',
  /** Postal address for Russian visitors. */
  ContactAddressRu: 'portal.contact.address.ru',
  /** Postal address for Armenian visitors. */
  ContactAddressHy: 'portal.contact.address.hy',
  /** Support working hours for Russian visitors. */
  ContactHoursRu: 'portal.contact.hours.ru',
  /** Support working hours for Armenian visitors. */
  ContactHoursHy: 'portal.contact.hours.hy',
  /** The storefront template. */
  Template: 'portal.template',
  /** Default colour mode of the storefront. */
  ThemeDefault: 'portal.theme.default',
  /** Whether visitors may switch the colour mode. */
  ThemeUserToggle: 'portal.theme.user_toggle',
  /** Logo drawn on light backgrounds. */
  Logo: 'portal.logo',
  /** Logo drawn on dark backgrounds. */
  LogoDark: 'portal.logo.dark',
  /** Square logo mark. */
  LogoMark: 'portal.logo.mark',
  /** Browser tab icon. */
  Favicon: 'portal.favicon',
  /** Primary brand colour. */
  BrandPrimary: 'portal.brand.primary',
  /** Accent brand colour. */
  BrandAccent: 'portal.brand.accent',
  /** Heading font. */
  BrandFontHeading: 'portal.brand.font_heading',
  /** Body font. */
  BrandFontBody: 'portal.brand.font_body',
  /** Tag manager container id. */
  AnalyticsGtmId: 'portal.analytics.gtm_id',
  /** Chat widget provider. */
  ChatProvider: 'portal.chat.provider',
  /** Chat server address. */
  ChatBaseUrl: 'portal.chat.base_url',
  /** Chat website token. */
  ChatWebsiteToken: 'portal.chat.website_token',
  /** Chat website token for Russian. */
  ChatWebsiteTokenRu: 'portal.chat.website_token.ru',
  /** Chat website token for Armenian. */
  ChatWebsiteTokenHy: 'portal.chat.website_token.hy',
  /** Whether the stats section shows on the home page. */
  SectionStats: 'portal.sections.stats',
  /** Whether the partners section shows on the home page. */
  SectionPartners: 'portal.sections.partners',
  /** Whether the services section shows on the home page. */
  SectionServices: 'portal.sections.services',
  /** Whether the process section shows on the home page. */
  SectionProcess: 'portal.sections.process',
  /** Whether the plans section shows on the home page. */
  SectionPlans: 'portal.sections.plans',
  /** Whether the comparison section shows on the home page. */
  SectionComparison: 'portal.sections.comparison',
  /** Whether the dashboard section shows on the home page. */
  SectionDashboard: 'portal.sections.dashboard',
  /** Whether the use cases section shows on the home page. */
  SectionUseCases: 'portal.sections.use_cases',
  /** Whether the migration section shows on the home page. */
  SectionMigration: 'portal.sections.migration',
  /** Whether the projects section shows on the home page. */
  SectionProjects: 'portal.sections.projects',
  /** Whether the testimonials section shows on the home page. */
  SectionTestimonials: 'portal.sections.testimonials',
  /** Whether the faq section shows on the home page. */
  SectionFaq: 'portal.sections.faq',
  /** Whether the cta section shows on the home page. */
  SectionCta: 'portal.sections.cta',
  /** Whether the locations section shows on the home page. */
  SectionLocations: 'portal.sections.locations',
  /** Whether the performance section shows on the home page. */
  SectionPerformance: 'portal.sections.performance',
  /** Whether the security section shows on the home page. */
  SectionSecurity: 'portal.sections.security',
  /** Whether the apps section shows on the home page. */
  SectionApps: 'portal.sections.apps',
  /** Whether the guarantees section shows on the home page. */
  SectionGuarantees: 'portal.sections.guarantees',
  /** Whether the support section shows on the home page. */
  SectionSupport: 'portal.sections.support',
  /** Whether the trusted section shows on the home page. */
  SectionTrusted: 'portal.sections.trusted',
  /** Response time figure in the performance section. */
  StatsResponseTime: 'portal.stats.response_time',
  /** Sites hosted figure in the performance section. */
  StatsSitesHosted: 'portal.stats.sites_hosted',
  /** Support response figure in the performance section. */
  StatsSupportResponse: 'portal.stats.support_response',
  /** Whether the security section lists free SSL. */
  FeaturesSsl: 'portal.features.ssl',
  /** Whether the security section lists DDoS protection. */
  FeaturesDdos: 'portal.features.ddos',
  /** Number of apps the one-click installer offers. */
  InstallerAppCount: 'portal.installer.app_count',
  /** Days of the money back guarantee. */
  GuaranteeMoneyBackDays: 'portal.guarantee.money_back_days',
  /** Whether the guarantees list no hidden fees. */
  GuaranteeNoHiddenFees: 'portal.guarantee.no_hidden_fees',
  /** Chat response time. */
  SupportResponseChat: 'portal.support.response.chat',
  /** Ticket response time. */
  SupportResponseTicket: 'portal.support.response.ticket',
  /** Email response time. */
  SupportResponseEmail: 'portal.support.response.email',
  /** Phone response time. */
  SupportResponsePhone: 'portal.support.response.phone',
} as const

/** One of the {@link SettingKey} values. */
export type SettingKey = (typeof SettingKey)[keyof typeof SettingKey]

/** Keys of the plugin system's settings; the strings are the rows' `key` values. */
export const PluginSettingKey = {
  /** Whether a production server still installs and loads plugins whose signature is missing or invalid. */
  AllowUnsigned: 'plugins.allow_unsigned',
} as const

/** One of the {@link PluginSettingKey} values. */
export type PluginSettingKey = (typeof PluginSettingKey)[keyof typeof PluginSettingKey]

/** Prefix of the environment variable a portal setting falls back to until its row exists. */
const SETTING_ENV_PREFIX = 'NUXT_PUBLIC_PORTAL_'

/** Environment variable the storefront falls back to for each setting, shown while the row is missing. */
export const SettingEnv: Record<SettingKey, string> = {
  [SettingKey.SiteName]: `${SETTING_ENV_PREFIX}SITE_NAME`,
  [SettingKey.SiteTagline]: `${SETTING_ENV_PREFIX}SITE_TAGLINE`,
  [SettingKey.StatsClients]: `${SETTING_ENV_PREFIX}STATS_CLIENTS`,
  [SettingKey.StatsUptime]: `${SETTING_ENV_PREFIX}STATS_UPTIME`,
  [SettingKey.StatsRegions]: `${SETTING_ENV_PREFIX}STATS_REGIONS`,
  [SettingKey.HeroBadge]: `${SETTING_ENV_PREFIX}HERO_BADGE`,
  [SettingKey.ContactEmail]: `${SETTING_ENV_PREFIX}CONTACT_EMAIL`,
  [SettingKey.ContactPhone]: `${SETTING_ENV_PREFIX}CONTACT_PHONE`,
  [SettingKey.ContactWhatsapp]: `${SETTING_ENV_PREFIX}CONTACT_WHATSAPP`,
  [SettingKey.ContactTelegram]: `${SETTING_ENV_PREFIX}CONTACT_TELEGRAM`,
  [SettingKey.ContactAddress]: `${SETTING_ENV_PREFIX}CONTACT_ADDRESS`,
  [SettingKey.ContactMapUrl]: `${SETTING_ENV_PREFIX}CONTACT_MAP_URL`,
  [SettingKey.ContactHours]: `${SETTING_ENV_PREFIX}CONTACT_HOURS`,
  [SettingKey.ContactTimezone]: `${SETTING_ENV_PREFIX}CONTACT_TIMEZONE`,
  [SettingKey.ContactAddressRu]: `${SETTING_ENV_PREFIX}CONTACT_ADDRESS_RU`,
  [SettingKey.ContactAddressHy]: `${SETTING_ENV_PREFIX}CONTACT_ADDRESS_HY`,
  [SettingKey.ContactHoursRu]: `${SETTING_ENV_PREFIX}CONTACT_HOURS_RU`,
  [SettingKey.ContactHoursHy]: `${SETTING_ENV_PREFIX}CONTACT_HOURS_HY`,
  [SettingKey.Template]: `${SETTING_ENV_PREFIX}TEMPLATE`,
  [SettingKey.ThemeDefault]: `${SETTING_ENV_PREFIX}THEME_DEFAULT`,
  [SettingKey.ThemeUserToggle]: `${SETTING_ENV_PREFIX}THEME_USER_TOGGLE`,
  [SettingKey.Logo]: `${SETTING_ENV_PREFIX}LOGO`,
  [SettingKey.LogoDark]: `${SETTING_ENV_PREFIX}LOGO_DARK`,
  [SettingKey.LogoMark]: `${SETTING_ENV_PREFIX}LOGO_MARK`,
  [SettingKey.Favicon]: `${SETTING_ENV_PREFIX}FAVICON`,
  [SettingKey.BrandPrimary]: `${SETTING_ENV_PREFIX}BRAND_PRIMARY`,
  [SettingKey.BrandAccent]: `${SETTING_ENV_PREFIX}BRAND_ACCENT`,
  [SettingKey.BrandFontHeading]: `${SETTING_ENV_PREFIX}BRAND_FONT_HEADING`,
  [SettingKey.BrandFontBody]: `${SETTING_ENV_PREFIX}BRAND_FONT_BODY`,
  [SettingKey.AnalyticsGtmId]: `${SETTING_ENV_PREFIX}GTM_ID`,
  [SettingKey.ChatProvider]: `${SETTING_ENV_PREFIX}CHAT_PROVIDER`,
  [SettingKey.ChatBaseUrl]: `${SETTING_ENV_PREFIX}CHAT_BASE_URL`,
  [SettingKey.ChatWebsiteToken]: `${SETTING_ENV_PREFIX}CHAT_WEBSITE_TOKEN`,
  [SettingKey.ChatWebsiteTokenRu]: `${SETTING_ENV_PREFIX}CHAT_WEBSITE_TOKEN_RU`,
  [SettingKey.ChatWebsiteTokenHy]: `${SETTING_ENV_PREFIX}CHAT_WEBSITE_TOKEN_HY`,
  [SettingKey.SectionStats]: `${SETTING_ENV_PREFIX}SECTIONS_STATS`,
  [SettingKey.SectionPartners]: `${SETTING_ENV_PREFIX}SECTIONS_PARTNERS`,
  [SettingKey.SectionServices]: `${SETTING_ENV_PREFIX}SECTIONS_SERVICES`,
  [SettingKey.SectionProcess]: `${SETTING_ENV_PREFIX}SECTIONS_PROCESS`,
  [SettingKey.SectionPlans]: `${SETTING_ENV_PREFIX}SECTIONS_PLANS`,
  [SettingKey.SectionComparison]: `${SETTING_ENV_PREFIX}SECTIONS_COMPARISON`,
  [SettingKey.SectionDashboard]: `${SETTING_ENV_PREFIX}SECTIONS_DASHBOARD`,
  [SettingKey.SectionUseCases]: `${SETTING_ENV_PREFIX}SECTIONS_USE_CASES`,
  [SettingKey.SectionMigration]: `${SETTING_ENV_PREFIX}SECTIONS_MIGRATION`,
  [SettingKey.SectionProjects]: `${SETTING_ENV_PREFIX}SECTIONS_PROJECTS`,
  [SettingKey.SectionTestimonials]: `${SETTING_ENV_PREFIX}SECTIONS_TESTIMONIALS`,
  [SettingKey.SectionFaq]: `${SETTING_ENV_PREFIX}SECTIONS_FAQ`,
  [SettingKey.SectionCta]: `${SETTING_ENV_PREFIX}SECTIONS_CTA`,
  [SettingKey.SectionLocations]: `${SETTING_ENV_PREFIX}SECTIONS_LOCATIONS`,
  [SettingKey.SectionPerformance]: `${SETTING_ENV_PREFIX}SECTIONS_PERFORMANCE`,
  [SettingKey.SectionSecurity]: `${SETTING_ENV_PREFIX}SECTIONS_SECURITY`,
  [SettingKey.SectionApps]: `${SETTING_ENV_PREFIX}SECTIONS_APPS`,
  [SettingKey.SectionGuarantees]: `${SETTING_ENV_PREFIX}SECTIONS_GUARANTEES`,
  [SettingKey.SectionSupport]: `${SETTING_ENV_PREFIX}SECTIONS_SUPPORT`,
  [SettingKey.SectionTrusted]: `${SETTING_ENV_PREFIX}SECTIONS_TRUSTED`,
  [SettingKey.StatsResponseTime]: `${SETTING_ENV_PREFIX}STATS_RESPONSE_TIME`,
  [SettingKey.StatsSitesHosted]: `${SETTING_ENV_PREFIX}STATS_SITES_HOSTED`,
  [SettingKey.StatsSupportResponse]: `${SETTING_ENV_PREFIX}STATS_SUPPORT_RESPONSE`,
  [SettingKey.FeaturesSsl]: `${SETTING_ENV_PREFIX}FEATURES_SSL`,
  [SettingKey.FeaturesDdos]: `${SETTING_ENV_PREFIX}FEATURES_DDOS`,
  [SettingKey.InstallerAppCount]: `${SETTING_ENV_PREFIX}INSTALLER_APP_COUNT`,
  [SettingKey.GuaranteeMoneyBackDays]: `${SETTING_ENV_PREFIX}GUARANTEE_MONEY_BACK_DAYS`,
  [SettingKey.GuaranteeNoHiddenFees]: `${SETTING_ENV_PREFIX}GUARANTEE_NO_HIDDEN_FEES`,
  [SettingKey.SupportResponseChat]: `${SETTING_ENV_PREFIX}SUPPORT_RESPONSE_CHAT`,
  [SettingKey.SupportResponseTicket]: `${SETTING_ENV_PREFIX}SUPPORT_RESPONSE_TICKET`,
  [SettingKey.SupportResponseEmail]: `${SETTING_ENV_PREFIX}SUPPORT_RESPONSE_EMAIL`,
  [SettingKey.SupportResponsePhone]: `${SETTING_ENV_PREFIX}SUPPORT_RESPONSE_PHONE`,
}

/** Closed set of the strings a boolean setting or integration toggle is stored as. */
export const SettingToggle = {
  /** The switch is on. */
  On: 'true',
  /** The switch is off. */
  Off: 'false',
} as const

/** One of the {@link SettingToggle} values. */
export type SettingToggle = (typeof SettingToggle)[keyof typeof SettingToggle]

/** What a card's save callback answers once the API has replied. */
export interface SettingSaveResult {
  /** Whether the API stored the value. */
  saved: boolean
  /** The API's own sentence when it refused; null on success. */
  error: string | null
}

/**
 * Persists one setting row on behalf of a card.
 *
 * @param id - Setting ID.
 * @param value - New value.
 * @returns The outcome, which the control shows next to itself.
 */
export type SettingSave = (id: number, value: string) => Promise<SettingSaveResult>
