/**
 * The `portal.*` settings the unauthenticated storefront endpoint may expose.
 *
 * An explicit list on purpose: deriving it from every `PortalSettingKey` would publish any key
 * added to that enum later, secret or not. A key is published only by being named here, and
 * `tests/constants/publicPortalSettingKeys.test.ts` pins the list to the backend's
 * `PortalSettingKeys.Public` set.
 *
 * @module constants/publicPortalSettingKeys
 */

import { localizedSettingKeys } from '~/utils/localizedSetting'
import { PortalSettingKey } from './portalSettingKey'

/**
 * The public settings that are written once per language. Each is public in every portal language, so the
 * list below carries the key of each language's row, named by `localizedSettingKeys`, not a literal per language.
 */
const LOCALIZED_PUBLIC_SETTINGS: readonly PortalSettingKey[] = [
  PortalSettingKey.ChatWebsiteToken,
  PortalSettingKey.AboutText,
  PortalSettingKey.ContactAddress,
  PortalSettingKey.ContactHours
]

/** The only setting keys `GET /api/portal/public/settings` will ever return. */
export const PUBLIC_PORTAL_SETTING_KEYS: readonly string[] = [
  PortalSettingKey.Template,
  PortalSettingKey.SiteName,
  PortalSettingKey.SiteTagline,
  PortalSettingKey.ThemeDefault,
  PortalSettingKey.ThemeUserToggle,
  PortalSettingKey.Logo,
  PortalSettingKey.LogoDark,
  PortalSettingKey.LogoMark,
  PortalSettingKey.Favicon,
  PortalSettingKey.BrandPrimary,
  PortalSettingKey.BrandAccent,
  PortalSettingKey.BrandFontHeading,
  PortalSettingKey.BrandFontBody,
  PortalSettingKey.AnalyticsGtmId,
  PortalSettingKey.ChatBaseUrl,
  PortalSettingKey.ChatProvider,
  PortalSettingKey.ContactWhatsapp,
  PortalSettingKey.ContactTelegram,
  PortalSettingKey.ContactEmail,
  PortalSettingKey.ContactPhone,
  PortalSettingKey.ContactTimezone,
  PortalSettingKey.ContactMapUrl,
  PortalSettingKey.NewsletterActionUrl,
  PortalSettingKey.SocialFacebook,
  PortalSettingKey.SocialInstagram,
  PortalSettingKey.SocialTwitter,
  PortalSettingKey.SocialLinkedin,
  PortalSettingKey.SocialYoutube,
  PortalSettingKey.StatsClients,
  PortalSettingKey.StatsUptime,
  PortalSettingKey.StatsRegions,
  PortalSettingKey.HeroBadge,
  PortalSettingKey.LegalTaxId,
  PortalSettingKey.AppsEnabled,
  PortalSettingKey.AppsAccount,
  PortalSettingKey.AppsTasks,
  PortalSettingKey.AppsErp,
  PortalSettingKey.AppsHostpanel,
  PortalSettingKey.AppsSheets,
  PortalSettingKey.AppsMail,
  PortalSettingKey.AppsDocs,
  PortalSettingKey.AppsCalendar,
  PortalSettingKey.FeaturesBackups,
  PortalSettingKey.FeaturesStorage,
  PortalSettingKey.FeaturesCaching,
  PortalSettingKey.FeaturesCdn,
  PortalSettingKey.FeaturesFirewall,
  PortalSettingKey.FeaturesMalware,
  PortalSettingKey.MigrationFree,
  PortalSettingKey.SectionStats,
  PortalSettingKey.SectionPartners,
  PortalSettingKey.SectionServices,
  PortalSettingKey.SectionProcess,
  PortalSettingKey.SectionPlans,
  PortalSettingKey.SectionComparison,
  PortalSettingKey.SectionDashboard,
  PortalSettingKey.SectionUseCases,
  PortalSettingKey.SectionMigration,
  PortalSettingKey.SectionProjects,
  PortalSettingKey.SectionTestimonials,
  PortalSettingKey.SectionFaq,
  PortalSettingKey.SectionCta,
  PortalSettingKey.SectionLocations,
  PortalSettingKey.SectionPerformance,
  PortalSettingKey.SectionSecurity,
  PortalSettingKey.SectionApps,
  PortalSettingKey.SectionGuarantees,
  PortalSettingKey.SectionSupport,
  PortalSettingKey.SectionTrusted,
  PortalSettingKey.StatsResponseTime,
  PortalSettingKey.StatsSitesHosted,
  PortalSettingKey.StatsSupportResponse,
  PortalSettingKey.FeaturesSsl,
  PortalSettingKey.FeaturesDdos,
  PortalSettingKey.InstallerAppCount,
  PortalSettingKey.GuaranteeMoneyBackDays,
  PortalSettingKey.GuaranteeNoHiddenFees,
  PortalSettingKey.SupportResponseChat,
  PortalSettingKey.SupportResponseTicket,
  PortalSettingKey.SupportResponseEmail,
  PortalSettingKey.SupportResponsePhone,
  ...LOCALIZED_PUBLIC_SETTINGS.flatMap(localizedSettingKeys)
]
