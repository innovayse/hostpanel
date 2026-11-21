/**
 * Keys of the operator-managed `portal.*` settings the storefront reads.
 *
 * @module constants/portalSettingKey
 */

/** Setting keys served by the public settings endpoint; kept in step with the backend list. */
export enum PortalSettingKey {
  /** Active storefront template. */
  Template = 'portal.template',
  /** Site name. */
  SiteName = 'portal.site.name',
  /** Site tagline. */
  SiteTagline = 'portal.site.tagline',
  /** Default colour theme. */
  ThemeDefault = 'portal.theme.default',
  /** Whether visitors may toggle the theme. */
  ThemeUserToggle = 'portal.theme.user_toggle',
  /** Logo. */
  Logo = 'portal.logo',
  /** Dark-mode logo. */
  LogoDark = 'portal.logo.dark',
  /** Logo mark. */
  LogoMark = 'portal.logo.mark',
  /** Favicon. */
  Favicon = 'portal.favicon',
  /** Primary brand colour. */
  BrandPrimary = 'portal.brand.primary',
  /** Accent brand colour. */
  BrandAccent = 'portal.brand.accent',
  /** Heading font. */
  BrandFontHeading = 'portal.brand.font_heading',
  /** Body font. */
  BrandFontBody = 'portal.brand.font_body',
  /** Tag manager id. */
  AnalyticsGtmId = 'portal.analytics.gtm_id',
  /** Live chat base URL. */
  ChatBaseUrl = 'portal.chat.base_url',
  /** Live chat website token. */
  ChatWebsiteToken = 'portal.chat.website_token',
  /** Live chat website token, Russian. */
  ChatWebsiteTokenRu = 'portal.chat.website_token.ru',
  /** Live chat website token, Armenian. */
  ChatWebsiteTokenHy = 'portal.chat.website_token.hy',
  /** Live chat provider. */
  ChatProvider = 'portal.chat.provider',
  /** WhatsApp number. */
  ContactWhatsapp = 'portal.contact.whatsapp',
  /** Telegram handle. */
  ContactTelegram = 'portal.contact.telegram',
  /** Contact email. */
  ContactEmail = 'portal.contact.email',
  /** Contact phone. */
  ContactPhone = 'portal.contact.phone',
  /** Office address. */
  ContactAddress = 'portal.contact.address',
  /** Opening hours text. */
  ContactHours = 'portal.contact.hours',
  /** Opening hours timezone. */
  ContactTimezone = 'portal.contact.timezone',
  /** Embedded map address. */
  ContactMapUrl = 'portal.contact.map_url',
  /** Newsletter form action. */
  NewsletterActionUrl = 'portal.newsletter.action_url',
  /** Facebook page. */
  SocialFacebook = 'portal.social.facebook',
  /** Instagram profile. */
  SocialInstagram = 'portal.social.instagram',
  /** Twitter profile. */
  SocialTwitter = 'portal.social.twitter',
  /** LinkedIn page. */
  SocialLinkedin = 'portal.social.linkedin',
  /** YouTube channel. */
  SocialYoutube = 'portal.social.youtube',
  /** Headline figure: clients. */
  StatsClients = 'portal.stats.clients',
  /** Headline figure: uptime. */
  StatsUptime = 'portal.stats.uptime',
  /** Headline figure: regions. */
  StatsRegions = 'portal.stats.regions',
  /** Hero badge line. */
  HeroBadge = 'portal.hero.badge',
  /** Tax id. */
  LegalTaxId = 'portal.legal.tax_id',
  /** Apps switch: all. */
  AppsEnabled = 'portal.apps.enabled',
  /** Apps switch: account. */
  AppsAccount = 'portal.apps.account',
  /** Apps switch: tasks. */
  AppsTasks = 'portal.apps.tasks',
  /** Apps switch: ERP. */
  AppsErp = 'portal.apps.erp',
  /** Apps switch: hostpanel. */
  AppsHostpanel = 'portal.apps.hostpanel',
  /** Apps switch: sheets. */
  AppsSheets = 'portal.apps.sheets',
  /** Apps switch: mail. */
  AppsMail = 'portal.apps.mail',
  /** Apps switch: docs. */
  AppsDocs = 'portal.apps.docs',
  /** Apps switch: calendar. */
  AppsCalendar = 'portal.apps.calendar',
  /** Backups feature switch. */
  FeaturesBackups = 'portal.features.backups',
  /** Storage feature switch. */
  FeaturesStorage = 'portal.features.storage',
  /** Caching feature switch. */
  FeaturesCaching = 'portal.features.caching',
  /** CDN feature switch. */
  FeaturesCdn = 'portal.features.cdn',
  /** Firewall feature switch. */
  FeaturesFirewall = 'portal.features.firewall',
  /** Malware scanning feature switch. */
  FeaturesMalware = 'portal.features.malware',
  /** Free migration offer switch. */
  MigrationFree = 'portal.migration.free',
  /** Company story on the About page, default language. */
  AboutText = 'portal.about.text',
  /** Company story for Russian visitors. */
  AboutTextRu = 'portal.about.text.ru',
  /** Company story for Armenian visitors. */
  AboutTextHy = 'portal.about.text.hy',
  /** The operator's own terms of service, default language. */
  LegalTerms = 'portal.legal.terms',
  /** The operator's own terms of service for Russian visitors. */
  LegalTermsRu = 'portal.legal.terms.ru',
  /** The operator's own terms of service for Armenian visitors. */
  LegalTermsHy = 'portal.legal.terms.hy',
  /** The operator's own privacy policy, default language. */
  LegalPrivacy = 'portal.legal.privacy',
  /** The operator's own privacy policy for Russian visitors. */
  LegalPrivacyRu = 'portal.legal.privacy.ru',
  /** The operator's own privacy policy for Armenian visitors. */
  LegalPrivacyHy = 'portal.legal.privacy.hy',
  /** The operator's own refund policy, default language. */
  LegalRefund = 'portal.legal.refund',
  /** The operator's own refund policy for Russian visitors. */
  LegalRefundRu = 'portal.legal.refund.ru',
  /** The operator's own refund policy for Armenian visitors. */
  LegalRefundHy = 'portal.legal.refund.hy',
  /** The operator's own acceptable use policy, default language. */
  LegalAcceptableUse = 'portal.legal.acceptable_use',
  /** The operator's own acceptable use policy for Russian visitors. */
  LegalAcceptableUseRu = 'portal.legal.acceptable_use.ru',
  /** The operator's own acceptable use policy for Armenian visitors. */
  LegalAcceptableUseHy = 'portal.legal.acceptable_use.hy',
  /** The operator's own cookie policy, default language. */
  LegalCookies = 'portal.legal.cookies',
  /** The operator's own cookie policy for Russian visitors. */
  LegalCookiesRu = 'portal.legal.cookies.ru',
  /** The operator's own cookie policy for Armenian visitors. */
  LegalCookiesHy = 'portal.legal.cookies.hy',
  /** Office address for Russian visitors. */
  ContactAddressRu = 'portal.contact.address.ru',
  /** Office address for Armenian visitors. */
  ContactAddressHy = 'portal.contact.address.hy',
  /** Support working hours for Russian visitors. */
  ContactHoursRu = 'portal.contact.hours.ru',
  /** Support working hours for Armenian visitors. */
  ContactHoursHy = 'portal.contact.hours.hy',
  /** Home section switch: stats strip. */
  SectionStats = 'portal.sections.stats',
  /** Home section switch: partners. */
  SectionPartners = 'portal.sections.partners',
  /** Home section switch: services. */
  SectionServices = 'portal.sections.services',
  /** Home section switch: process. */
  SectionProcess = 'portal.sections.process',
  /** Home section switch: plans. */
  SectionPlans = 'portal.sections.plans',
  /** Home section switch: comparison. */
  SectionComparison = 'portal.sections.comparison',
  /** Home section switch: dashboard. */
  SectionDashboard = 'portal.sections.dashboard',
  /** Home section switch: use cases. */
  SectionUseCases = 'portal.sections.use_cases',
  /** Home section switch: migration. */
  SectionMigration = 'portal.sections.migration',
  /** Home section switch: projects. */
  SectionProjects = 'portal.sections.projects',
  /** Home section switch: testimonials. */
  SectionTestimonials = 'portal.sections.testimonials',
  /** Home section switch: questions. */
  SectionFaq = 'portal.sections.faq',
  /** Home section switch: closing call to action. */
  SectionCta = 'portal.sections.cta',
  /** Home section switch: server locations. */
  SectionLocations = 'portal.sections.locations',
  /** Home section switch: performance. */
  SectionPerformance = 'portal.sections.performance',
  /** Home section switch: security. */
  SectionSecurity = 'portal.sections.security',
  /** Home section switch: one-click installer. */
  SectionApps = 'portal.sections.apps',
  /** Home section switch: guarantees. */
  SectionGuarantees = 'portal.sections.guarantees',
  /** Home section switch: support channels. */
  SectionSupport = 'portal.sections.support',
  /** Home section switch: trusted-by logo strip. */
  SectionTrusted = 'portal.sections.trusted',
  /** Performance figure: server response time. */
  StatsResponseTime = 'portal.stats.response_time',
  /** Performance figure: sites hosted. */
  StatsSitesHosted = 'portal.stats.sites_hosted',
  /** Performance figure: support response time. */
  StatsSupportResponse = 'portal.stats.support_response',
  /** Free SSL feature switch. */
  FeaturesSsl = 'portal.features.ssl',
  /** DDoS protection feature switch. */
  FeaturesDdos = 'portal.features.ddos',
  /** Number of apps the one-click installer offers. */
  InstallerAppCount = 'portal.installer.app_count',
  /** Money-back guarantee period in days. */
  GuaranteeMoneyBackDays = 'portal.guarantee.money_back_days',
  /** No-hidden-fees guarantee switch. */
  GuaranteeNoHiddenFees = 'portal.guarantee.no_hidden_fees',
  /** Chat response time. */
  SupportResponseChat = 'portal.support.response.chat',
  /** Ticket response time. */
  SupportResponseTicket = 'portal.support.response.ticket',
  /** Email response time. */
  SupportResponseEmail = 'portal.support.response.email',
  /** Phone response time. */
  SupportResponsePhone = 'portal.support.response.phone',
}
