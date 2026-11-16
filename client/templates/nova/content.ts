/**
 * nova's structure, in one place.
 *
 * Every list below is what the sections iterate: adding a use case or a feature
 * card is a line here plus its i18n keys in the three locale
 * files, and no component changes. The strings themselves are never here, only
 * the keys, so a translator never has to open a `.ts` file.
 *
 * @module templates/nova/content
 */

import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { GuaranteeKey } from '~/constants/guaranteeKey'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { PerformanceStatKey } from '~/constants/performanceStatKey'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'
import { SupportChannel } from '~/constants/supportChannel'
import type { DashboardRow } from '~/templates/nova/types/dashboardrow'
import type { Feature } from '~/templates/nova/types/feature'
import type { MigrationStep } from '~/templates/nova/types/migrationstep'
import type { NavItem } from '~/templates/nova/types/navitem'
import type { UseCase } from '~/templates/nova/types/usecase'
import type { ComparisonRow } from '~/types/comparisonrow'
import {
  SITE_ABOUT_PATH,
  SITE_ACCEPTABLE_USE_PATH,
  SITE_ANNOUNCEMENTS_PATH,
  SITE_CONTACT_PATH,
  SITE_DOMAINS_PATH,
  SITE_DOMAINS_TRANSFER_PATH,
  SITE_FAQ_PATH,
  SITE_HOSTING_PATH,
  SITE_HOSTING_PLANS_PATH,
  SITE_KNOWLEDGEBASE_PATH,
  SITE_PRIVACY_PATH,
  SITE_PRODUCTS_PATH,
  SITE_REFUND_POLICY_PATH,
  SITE_TERMS_PATH,
} from '~/constants/siteRoutes'

/**
 * Primary navigation.
 *
 * `/websites`, `/email` and `/pricing` do not exist in this portal, so the
 * entries that would carry those names point at the routes that answer them:
 * the product catalogue for both product lines, and the hosting page's plan
 * anchor for pricing. Linking to routes named after the labels would put
 * three 404s in the header of every page.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { key: 'hosting', labelKey: 'nova.nav.hosting', to: SITE_HOSTING_PATH },
  { key: 'domains', labelKey: 'nova.nav.domains', to: SITE_DOMAINS_PATH },
  { key: 'websites', labelKey: 'nova.nav.websites', to: SITE_PRODUCTS_PATH },
  { key: 'email', labelKey: 'nova.nav.email', to: SITE_PRODUCTS_PATH },
  { key: 'pricing', labelKey: 'nova.nav.pricing', to: SITE_HOSTING_PLANS_PATH },
  { key: 'support', labelKey: 'nova.nav.support', to: SITE_KNOWLEDGEBASE_PATH },
]

/**
 * The hero's benefit list.
 *
 * Titles only: the hero is not the place to argue a point, and every line here
 * is expanded on further down the page. `requiresSetting` gates the two claims
 * this codebase cannot check for itself.
 */
export const HERO_BENEFITS: readonly Feature[] = [
  { key: 'ssl', icon: 'lucide:lock', titleKey: 'nova.hero.benefitSsl' },
  {
    key: 'backups',
    icon: 'lucide:history',
    titleKey: 'nova.hero.benefitBackups',
    requiresSetting: PortalSettingKey.FeaturesBackups,
  },
  { key: 'support', icon: 'lucide:life-buoy', titleKey: 'nova.hero.benefitSupport' },
  { key: 'scale', icon: 'lucide:trending-up', titleKey: 'nova.hero.benefitScale' },
]

/**
 * The trust bar under the hero.
 *
 * Qualitative on purpose. An uptime figure is a measurement, and nothing in
 * this repository measures it, so the one numeric line is gated behind an
 * operator setting that holds the figure itself: `portal.stats.uptime`, the same figure the hero prints.
 */
export const TRUST_ITEMS: readonly Feature[] = [
  { key: 'performance', icon: 'lucide:zap', titleKey: 'nova.trust.performance' },
  { key: 'infrastructure', icon: 'lucide:server', titleKey: 'nova.trust.infrastructure' },
  { key: 'ssl', icon: 'lucide:shield-check', titleKey: 'nova.trust.ssl' },
  {
    key: 'backups',
    icon: 'lucide:database-backup',
    titleKey: 'nova.trust.backups',
    requiresSetting: PortalSettingKey.FeaturesBackups,
  },
  { key: 'support', icon: 'lucide:headphones', titleKey: 'nova.trust.support' },
]

/** The "why choose us" grid. Every line here is something the platform does. */
export const WHY_FEATURES: readonly Feature[] = [
  {
    key: 'performance',
    icon: 'lucide:gauge',
    titleKey: 'nova.why.performanceTitle',
    bodyKey: 'nova.why.performanceBody',
  },
  {
    key: 'security',
    icon: 'lucide:shield-check',
    titleKey: 'nova.why.securityTitle',
    bodyKey: 'nova.why.securityBody',
  },
  {
    key: 'management',
    icon: 'lucide:layout-dashboard',
    titleKey: 'nova.why.managementTitle',
    bodyKey: 'nova.why.managementBody',
  },
  {
    key: 'automation',
    icon: 'lucide:workflow',
    titleKey: 'nova.why.automationTitle',
    bodyKey: 'nova.why.automationBody',
  },
  {
    key: 'developers',
    icon: 'lucide:terminal',
    titleKey: 'nova.why.developersTitle',
    bodyKey: 'nova.why.developersBody',
  },
  {
    key: 'support',
    icon: 'lucide:message-circle',
    titleKey: 'nova.why.supportTitle',
    bodyKey: 'nova.why.supportBody',
  },
]

/**
 * The performance section.
 *
 * Storage medium, caching and a CDN are properties of the machines an operator
 * runs, not of this application: it provisions accounts through a hosting
 * control panel and never learns what is underneath. Those three are gated. What is left
 * ungated is true of every install: plans can be upgraded, provisioning is
 * automated, and the operator chooses the server an account lands on.
 */
export const PERFORMANCE_FEATURES: readonly Feature[] = [
  {
    key: 'storage',
    icon: 'lucide:hard-drive',
    titleKey: 'nova.performance.storageTitle',
    bodyKey: 'nova.performance.storageBody',
    requiresSetting: PortalSettingKey.FeaturesStorage,
  },
  {
    key: 'caching',
    icon: 'lucide:rocket',
    titleKey: 'nova.performance.cachingTitle',
    bodyKey: 'nova.performance.cachingBody',
    requiresSetting: PortalSettingKey.FeaturesCaching,
  },
  {
    key: 'cdn',
    icon: 'lucide:globe',
    titleKey: 'nova.performance.cdnTitle',
    bodyKey: 'nova.performance.cdnBody',
    requiresSetting: PortalSettingKey.FeaturesCdn,
  },
  {
    key: 'servers',
    icon: 'lucide:server-cog',
    titleKey: 'nova.performance.serversTitle',
    bodyKey: 'nova.performance.serversBody',
  },
  {
    key: 'scaling',
    icon: 'lucide:move-up-right',
    titleKey: 'nova.performance.scalingTitle',
    bodyKey: 'nova.performance.scalingBody',
  },
]

/**
 * The security statements that are true of every install.
 *
 * Two-factor sign-in with an audit log, and per-account isolation, are implemented in this
 * codebase and stated plainly. The five operator-configured claims (SSL, backups, DDoS, malware,
 * firewall) are not here: they wait on their settings and live in {@link SECURITY_FEATURE_CARDS}.
 */
export const SECURITY_FEATURES: readonly Feature[] = [
  {
    key: 'account',
    icon: 'lucide:key-round',
    titleKey: 'nova.security.accountTitle',
    bodyKey: 'nova.security.accountBody',
  },
  {
    key: 'isolation',
    icon: 'lucide:box',
    titleKey: 'nova.security.isolationTitle',
    bodyKey: 'nova.security.isolationBody',
  },
]

/**
 * The card of each operator-configured security claim.
 *
 * Which of them render is decided by `useSecurityFeatures()`; this table only says how each one
 * looks, so the SSL, backup, firewall and malware wording is stated once, here, and not in
 * {@link SECURITY_FEATURES}.
 */
export const SECURITY_FEATURE_CARDS: Readonly<Record<SecurityFeatureKey, Feature>> = {
  [SecurityFeatureKey.Ssl]: {
    key: SecurityFeatureKey.Ssl,
    icon: 'lucide:lock',
    titleKey: 'nova.security.sslTitle',
    bodyKey: 'nova.security.sslBody',
  },
  [SecurityFeatureKey.Backups]: {
    key: SecurityFeatureKey.Backups,
    icon: 'lucide:database-backup',
    titleKey: 'nova.security.backupsTitle',
    bodyKey: 'nova.security.backupsBody',
  },
  [SecurityFeatureKey.Ddos]: {
    key: SecurityFeatureKey.Ddos,
    icon: 'lucide:shield-alert',
    titleKey: 'nova.security.ddosTitle',
    bodyKey: 'nova.security.ddosBody',
  },
  [SecurityFeatureKey.Malware]: {
    key: SecurityFeatureKey.Malware,
    icon: 'lucide:bug',
    titleKey: 'nova.security.malwareTitle',
    bodyKey: 'nova.security.malwareBody',
  },
  [SecurityFeatureKey.Firewall]: {
    key: SecurityFeatureKey.Firewall,
    icon: 'lucide:shield',
    titleKey: 'nova.security.firewallTitle',
    bodyKey: 'nova.security.firewallBody',
  },
}

/** The tile of each performance figure: icon and label key. Values come from `usePerformanceStats()`. */
export const PERFORMANCE_STAT_CARDS: Readonly<Record<PerformanceStatKey, Feature>> = {
  [PerformanceStatKey.Uptime]: { key: PerformanceStatKey.Uptime, icon: 'lucide:activity', titleKey: 'nova.performance.statUptime' },
  [PerformanceStatKey.ResponseTime]: { key: PerformanceStatKey.ResponseTime, icon: 'lucide:timer', titleKey: 'nova.performance.statResponseTime' },
  [PerformanceStatKey.SitesHosted]: { key: PerformanceStatKey.SitesHosted, icon: 'lucide:globe', titleKey: 'nova.performance.statSitesHosted' },
  [PerformanceStatKey.SupportResponse]: { key: PerformanceStatKey.SupportResponse, icon: 'lucide:headphones', titleKey: 'nova.performance.statSupportResponse' },
}

/** The card of each guarantee. The money-back body takes `{days}`. */
export const GUARANTEE_CARDS: Readonly<Record<GuaranteeKey, Feature>> = {
  [GuaranteeKey.FreeMigration]: {
    key: GuaranteeKey.FreeMigration,
    icon: 'lucide:truck',
    titleKey: 'nova.guarantees.freeMigrationTitle',
    bodyKey: 'nova.guarantees.freeMigrationBody',
  },
  [GuaranteeKey.MoneyBack]: {
    key: GuaranteeKey.MoneyBack,
    icon: 'lucide:badge-check',
    titleKey: 'nova.guarantees.moneyBackTitle',
    bodyKey: 'nova.guarantees.moneyBackBody',
  },
  [GuaranteeKey.NoHiddenFees]: {
    key: GuaranteeKey.NoHiddenFees,
    icon: 'lucide:receipt',
    titleKey: 'nova.guarantees.noHiddenFeesTitle',
    bodyKey: 'nova.guarantees.noHiddenFeesBody',
  },
}

/** The card of each support channel. The title is the channel name; the body says what it is for. */
export const SUPPORT_CHANNEL_CARDS: Readonly<Record<SupportChannel, Feature>> = {
  [SupportChannel.Chat]: {
    key: SupportChannel.Chat,
    icon: 'lucide:message-circle',
    titleKey: 'nova.support.chatTitle',
    bodyKey: 'nova.support.chatBody',
  },
  [SupportChannel.Ticket]: {
    key: SupportChannel.Ticket,
    icon: 'lucide:ticket',
    titleKey: 'nova.support.ticketTitle',
    bodyKey: 'nova.support.ticketBody',
  },
  [SupportChannel.Email]: {
    key: SupportChannel.Email,
    icon: 'lucide:mail',
    titleKey: 'nova.support.emailTitle',
    bodyKey: 'nova.support.emailBody',
  },
  [SupportChannel.Phone]: {
    key: SupportChannel.Phone,
    icon: 'lucide:phone',
    titleKey: 'nova.support.phoneTitle',
    bodyKey: 'nova.support.phoneBody',
  },
}

/** The one-click installer categories: icon and label key. */
export const INSTALLER_CATEGORY_CARDS: Readonly<Record<InstallerAppCategory, Feature>> = {
  [InstallerAppCategory.Cms]: { key: InstallerAppCategory.Cms, icon: 'lucide:layout-template', titleKey: 'nova.apps.cms' },
  [InstallerAppCategory.Shop]: { key: InstallerAppCategory.Shop, icon: 'lucide:shopping-cart', titleKey: 'nova.apps.shop' },
  [InstallerAppCategory.Forum]: { key: InstallerAppCategory.Forum, icon: 'lucide:messages-square', titleKey: 'nova.apps.forum' },
  [InstallerAppCategory.Wiki]: { key: InstallerAppCategory.Wiki, icon: 'lucide:book-open', titleKey: 'nova.apps.wiki' },
  [InstallerAppCategory.Blog]: { key: InstallerAppCategory.Blog, icon: 'lucide:pen-line', titleKey: 'nova.apps.blog' },
  [InstallerAppCategory.Gallery]: { key: InstallerAppCategory.Gallery, icon: 'lucide:image', titleKey: 'nova.apps.gallery' },
}

/**
 * Rows of the control-panel preview.
 *
 * The preview is drawn in CSS from these rows rather than shipped as a
 * screenshot: no screenshot of this panel exists in the repository, and a stock
 * image of somebody else's dashboard would be a picture of a product the
 * visitor is not buying. Every row names an area the client area really has.
 */
export const DASHBOARD_ROWS: readonly DashboardRow[] = [
  { key: 'websites', icon: 'lucide:layout-template', labelKey: 'nova.dashboard.websites', valueKey: 'nova.dashboard.websitesValue' },
  { key: 'domains', icon: 'lucide:globe', labelKey: 'nova.dashboard.domains', valueKey: 'nova.dashboard.domainsValue' },
  { key: 'databases', icon: 'lucide:database', labelKey: 'nova.dashboard.databases', valueKey: 'nova.dashboard.databasesValue' },
  { key: 'files', icon: 'lucide:folder', labelKey: 'nova.dashboard.files', valueKey: 'nova.dashboard.filesValue' },
  { key: 'ssl', icon: 'lucide:lock', labelKey: 'nova.dashboard.ssl', valueKey: 'nova.dashboard.sslValue' },
  { key: 'backups', icon: 'lucide:history', labelKey: 'nova.dashboard.backups', valueKey: 'nova.dashboard.backupsValue' },
  { key: 'email', icon: 'lucide:mail', labelKey: 'nova.dashboard.email', valueKey: 'nova.dashboard.emailValue' },
]

/** The four steps of the migration walkthrough. */
export const MIGRATION_STEPS: readonly MigrationStep[] = [
  { key: 'plan', titleKey: 'nova.migration.step1Title', bodyKey: 'nova.migration.step1Body' },
  { key: 'send', titleKey: 'nova.migration.step2Title', bodyKey: 'nova.migration.step2Body' },
  { key: 'move', titleKey: 'nova.migration.step3Title', bodyKey: 'nova.migration.step3Body' },
  { key: 'live', titleKey: 'nova.migration.step4Title', bodyKey: 'nova.migration.step4Body' },
]

/**
 * Setting that turns the "free migration" badge on.
 *
 * Whether a migration is free is a commercial decision an operator makes, and
 * nothing in the backend records it, so the badge waits to be switched on.
 */
export const MIGRATION_FREE_SETTING = PortalSettingKey.MigrationFree

/** The audiences grid. */
export const USE_CASES: readonly UseCase[] = [
  { key: 'personal', icon: 'lucide:user', titleKey: 'nova.useCases.personalTitle', bodyKey: 'nova.useCases.personalBody' },
  { key: 'business', icon: 'lucide:building-2', titleKey: 'nova.useCases.businessTitle', bodyKey: 'nova.useCases.businessBody' },
  { key: 'wordpress', icon: 'lucide:file-text', titleKey: 'nova.useCases.wordpressTitle', bodyKey: 'nova.useCases.wordpressBody' },
  { key: 'ecommerce', icon: 'lucide:shopping-bag', titleKey: 'nova.useCases.ecommerceTitle', bodyKey: 'nova.useCases.ecommerceBody' },
  { key: 'agencies', icon: 'lucide:users', titleKey: 'nova.useCases.agenciesTitle', bodyKey: 'nova.useCases.agenciesBody' },
  { key: 'developers', icon: 'lucide:code-2', titleKey: 'nova.useCases.developersTitle', bodyKey: 'nova.useCases.developersBody' },
]

/** One footer column: a heading and the links beneath it. */
export interface FooterColumn {
  /** Stable key, used for the list key. */
  key: string
  /** i18n key for the column heading. */
  titleKey: string
  /** The links beneath the heading. */
  links: readonly NavItem[]
}

/**
 * Footer columns.
 *
 * Only routes this portal has. `/about` tells the operator's story, or sends the
 * visitor on to contact when none is set, the same link aurora's footer uses.
 */
export const FOOTER_COLUMNS: readonly FooterColumn[] = [
  {
    key: 'hosting',
    titleKey: 'nova.footer.hosting',
    links: [
      { key: 'web', labelKey: 'nova.footer.webHosting', to: SITE_HOSTING_PATH },
      { key: 'wordpress', labelKey: 'nova.footer.wordpressHosting', to: SITE_HOSTING_PATH },
      { key: 'pricing', labelKey: 'nova.footer.pricing', to: SITE_HOSTING_PLANS_PATH },
      { key: 'trial', labelKey: 'nova.footer.trial', to: SITE_PRODUCTS_PATH },
    ],
  },
  {
    key: 'domains',
    titleKey: 'nova.footer.domains',
    links: [
      { key: 'register', labelKey: 'nova.footer.domainRegister', to: SITE_DOMAINS_PATH },
      { key: 'transfer', labelKey: 'nova.footer.domainTransfer', to: SITE_DOMAINS_TRANSFER_PATH },
    ],
  },
  {
    key: 'products',
    titleKey: 'nova.footer.products',
    links: [
      { key: 'catalogue', labelKey: 'nova.footer.catalogue', to: SITE_PRODUCTS_PATH },
      { key: 'email', labelKey: 'nova.footer.email', to: SITE_PRODUCTS_PATH },
    ],
  },
  {
    key: 'company',
    titleKey: 'nova.footer.company',
    links: [
      { key: 'about', labelKey: 'nova.footer.about', to: SITE_ABOUT_PATH },
      { key: 'contact', labelKey: 'nova.footer.contact', to: SITE_CONTACT_PATH },
      { key: 'announcements', labelKey: 'nova.footer.announcements', to: SITE_ANNOUNCEMENTS_PATH },
    ],
  },
  {
    key: 'support',
    titleKey: 'nova.footer.support',
    links: [
      { key: 'help', labelKey: 'nova.footer.helpCentre', to: SITE_KNOWLEDGEBASE_PATH },
      { key: 'faq', labelKey: 'nova.footer.faq', to: SITE_FAQ_PATH },
      { key: 'login', labelKey: 'nova.footer.clientArea', to: CLIENT_LOGIN_PATH },
    ],
  },
  {
    key: 'legal',
    titleKey: 'nova.footer.legal',
    links: [
      { key: 'terms', labelKey: 'nova.footer.terms', to: SITE_TERMS_PATH },
      { key: 'privacy', labelKey: 'nova.footer.privacy', to: SITE_PRIVACY_PATH },
      { key: 'refund', labelKey: 'nova.footer.refund', to: SITE_REFUND_POLICY_PATH },
      { key: 'aup', labelKey: 'nova.footer.aup', to: SITE_ACCEPTABLE_USE_PATH },
    ],
  },
]

/**
 * Preferred order for the comparison table's rows.
 *
 * The rows themselves are whatever an operator entered under Admin → Products →
 * Specification, so this list cannot add one: it only says which order the
 * familiar lines read best in when they are present. Matching is
 * case-insensitive on the label, and anything unrecognised (a line in Armenian,
 * a line this list never anticipated) keeps its original position after the
 * ones that matched, rather than being dropped.
 */
export const COMPARISON_LABEL_ORDER: readonly string[] = [
  'websites',
  'storage',
  'bandwidth',
  'cpu',
  'ram',
  'ssl',
  'backups',
  'cdn',
  'email',
  'databases',
  'support',
  'migration',
]

/**
 * Sorts comparison rows into {@link COMPARISON_LABEL_ORDER}.
 *
 * @param rows Rows as `buildComparisonRows` aligned them.
 * @returns The same rows, reordered. Never more, never fewer.
 */
export const orderComparisonRows = (rows: readonly ComparisonRow[]): ComparisonRow[] => {
  /**
   * Position of a label in the preferred order; unknown labels rank after every known one.
   *
   * @param label Row label as the operator entered it.
   * @returns The index in {@link COMPARISON_LABEL_ORDER}, or its length when unknown.
   */
  const rank = (label: string): number => {
    const normalised = label.trim().toLowerCase()
    const index = COMPARISON_LABEL_ORDER.findIndex(known => normalised.includes(known))
    return index === -1 ? COMPARISON_LABEL_ORDER.length : index
  }

  // Index carried alongside so the sort is stable across engines: rows that
  // rank the same must stay in the order the operator's sortOrder put them.
  return rows
    .map((row, index) => ({ row, index, rank: rank(row.label) }))
    .sort((a, b) => a.rank - b.rank || a.index - b.index)
    .map(entry => entry.row)
}
