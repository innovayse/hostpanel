import type {
  ComparisonRow,
  DashboardRow,
  FaqItem,
  Feature,
  MigrationStep,
  NavItem,
  Testimonial,
  UseCase,
} from '~/templates/nova/types'

/**
 * nova's structure, in one place.
 *
 * Every list below is what the sections iterate: adding a use case, a feature
 * card or an FAQ entry is a line here plus its i18n keys in the three locale
 * files, and no component changes. The strings themselves are never here — only
 * the keys — so a translator never has to open a `.ts` file.
 */

/**
 * Primary navigation.
 *
 * `/websites`, `/email` and `/pricing` do not exist in this portal, so the
 * entries that would carry those names point at the routes that answer them:
 * the product catalogue for both product lines, and the hosting page's plan
 * anchor for pricing. Inventing the routes to match the labels would have
 * shipped three 404s off the header of every page.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { key: 'hosting', labelKey: 'nova.nav.hosting', to: '/hosting' },
  { key: 'domains', labelKey: 'nova.nav.domains', to: '/domains' },
  { key: 'websites', labelKey: 'nova.nav.websites', to: '/products' },
  { key: 'email', labelKey: 'nova.nav.email', to: '/products' },
  { key: 'pricing', labelKey: 'nova.nav.pricing', to: '/hosting#plans' },
  { key: 'support', labelKey: 'nova.nav.support', to: '/knowledgebase' },
]

/**
 * The hero's benefit list.
 *
 * Titles only — the hero is not the place to argue a point, and every line here
 * is expanded on further down the page. `requiresSetting` gates the two claims
 * this codebase cannot check for itself.
 */
export const HERO_BENEFITS: readonly Feature[] = [
  { key: 'ssl', icon: 'lucide:lock', titleKey: 'nova.hero.benefitSsl' },
  {
    key: 'backups',
    icon: 'lucide:history',
    titleKey: 'nova.hero.benefitBackups',
    requiresSetting: ['portal.features.backups', 'portalFeatureBackups'],
  },
  { key: 'support', icon: 'lucide:life-buoy', titleKey: 'nova.hero.benefitSupport' },
  { key: 'scale', icon: 'lucide:trending-up', titleKey: 'nova.hero.benefitScale' },
]

/**
 * The trust bar under the hero.
 *
 * Qualitative on purpose. An uptime figure is a measurement, and nothing in
 * this repository measures it, so the one numeric line is gated behind an
 * operator setting that holds the figure itself — see `TRUST_UPTIME_SETTING`.
 */
export const TRUST_ITEMS: readonly Feature[] = [
  { key: 'performance', icon: 'lucide:zap', titleKey: 'nova.trust.performance' },
  { key: 'infrastructure', icon: 'lucide:server', titleKey: 'nova.trust.infrastructure' },
  { key: 'ssl', icon: 'lucide:shield-check', titleKey: 'nova.trust.ssl' },
  {
    key: 'backups',
    icon: 'lucide:database-backup',
    titleKey: 'nova.trust.backups',
    requiresSetting: ['portal.features.backups', 'portalFeatureBackups'],
  },
  { key: 'support', icon: 'lucide:headphones', titleKey: 'nova.trust.support' },
]

/**
 * Setting that holds the operator's published uptime figure, e.g. `99.9%`.
 *
 * The trust bar prints it verbatim and shows nothing when it is unset. It is a
 * setting rather than a constant because it is a promise a particular operator
 * makes about particular hardware, and this project has no standing to make it
 * on their behalf.
 */
export const TRUST_UPTIME_SETTING = ['portal.trust.uptime', 'portalTrustUptime'] as const

/** The "why Innovayse" grid. Every line here is something the platform does. */
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
 * runs, not of this application: it provisions accounts through cPanel/WHM or
 * CWP and never learns what is underneath. Those three are gated. What is left
 * ungated is true of every install — plans can be upgraded, provisioning is
 * automated, and the operator chooses the server an account lands on.
 */
export const PERFORMANCE_FEATURES: readonly Feature[] = [
  {
    key: 'storage',
    icon: 'lucide:hard-drive',
    titleKey: 'nova.performance.storageTitle',
    bodyKey: 'nova.performance.storageBody',
    requiresSetting: ['portal.features.storage', 'portalFeatureStorage'],
  },
  {
    key: 'caching',
    icon: 'lucide:rocket',
    titleKey: 'nova.performance.cachingTitle',
    bodyKey: 'nova.performance.cachingBody',
    requiresSetting: ['portal.features.caching', 'portalFeatureCaching'],
  },
  {
    key: 'cdn',
    icon: 'lucide:globe',
    titleKey: 'nova.performance.cdnTitle',
    bodyKey: 'nova.performance.cdnBody',
    requiresSetting: ['portal.features.cdn', 'portalFeatureCdn'],
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
 * The security section.
 *
 * SSL certificates and two-factor sign-in are implemented in this codebase and
 * stated plainly. A firewall, a malware scanner and a backup schedule belong to
 * the operator's servers, so each waits on its setting.
 */
export const SECURITY_FEATURES: readonly Feature[] = [
  {
    key: 'ssl',
    icon: 'lucide:lock',
    titleKey: 'nova.security.sslTitle',
    bodyKey: 'nova.security.sslBody',
  },
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
  {
    key: 'backups',
    icon: 'lucide:database-backup',
    titleKey: 'nova.security.backupsTitle',
    bodyKey: 'nova.security.backupsBody',
    requiresSetting: ['portal.features.backups', 'portalFeatureBackups'],
  },
  {
    key: 'firewall',
    icon: 'lucide:shield',
    titleKey: 'nova.security.firewallTitle',
    bodyKey: 'nova.security.firewallBody',
    requiresSetting: ['portal.features.firewall', 'portalFeatureFirewall'],
  },
  {
    key: 'malware',
    icon: 'lucide:bug',
    titleKey: 'nova.security.malwareTitle',
    bodyKey: 'nova.security.malwareBody',
    requiresSetting: ['portal.features.malware', 'portalFeatureMalware'],
  },
]

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
export const MIGRATION_FREE_SETTING = ['portal.migration.free', 'portalMigrationFree'] as const

/** The audiences grid. */
export const USE_CASES: readonly UseCase[] = [
  { key: 'personal', icon: 'lucide:user', titleKey: 'nova.useCases.personalTitle', bodyKey: 'nova.useCases.personalBody' },
  { key: 'business', icon: 'lucide:building-2', titleKey: 'nova.useCases.businessTitle', bodyKey: 'nova.useCases.businessBody' },
  { key: 'wordpress', icon: 'lucide:file-text', titleKey: 'nova.useCases.wordpressTitle', bodyKey: 'nova.useCases.wordpressBody' },
  { key: 'ecommerce', icon: 'lucide:shopping-bag', titleKey: 'nova.useCases.ecommerceTitle', bodyKey: 'nova.useCases.ecommerceBody' },
  { key: 'agencies', icon: 'lucide:users', titleKey: 'nova.useCases.agenciesTitle', bodyKey: 'nova.useCases.agenciesBody' },
  { key: 'developers', icon: 'lucide:code-2', titleKey: 'nova.useCases.developersTitle', bodyKey: 'nova.useCases.developersBody' },
]

/**
 * Customer quotes.
 *
 * Deliberately empty: no endpoint serves testimonials, and the section renders
 * nothing while this list is. Filling it means pasting words somebody actually
 * said, with their name as they gave it — the shape exists so that stays the
 * only way to make the section appear.
 */
export const TESTIMONIALS: readonly Testimonial[] = []

/** The FAQ accordion. */
export const FAQ_ITEMS: readonly FaqItem[] = [
  { key: 'what', questionKey: 'nova.faq.whatQ', answerKey: 'nova.faq.whatA' },
  { key: 'which', questionKey: 'nova.faq.whichQ', answerKey: 'nova.faq.whichA' },
  { key: 'migrate', questionKey: 'nova.faq.migrateQ', answerKey: 'nova.faq.migrateA' },
  { key: 'ssl', questionKey: 'nova.faq.sslQ', answerKey: 'nova.faq.sslA' },
  { key: 'backups', questionKey: 'nova.faq.backupsQ', answerKey: 'nova.faq.backupsA' },
  { key: 'wordpress', questionKey: 'nova.faq.wordpressQ', answerKey: 'nova.faq.wordpressA' },
  { key: 'upgrade', questionKey: 'nova.faq.upgradeQ', answerKey: 'nova.faq.upgradeA' },
  { key: 'expires', questionKey: 'nova.faq.expiresQ', answerKey: 'nova.faq.expiresA' },
  { key: 'contact', questionKey: 'nova.faq.contactQ', answerKey: 'nova.faq.contactA' },
]

/** One footer column: a heading and the links beneath it. */
export interface FooterColumn {
  key: string
  titleKey: string
  links: readonly NavItem[]
}

/**
 * Footer columns.
 *
 * Only routes this portal has. There is no `/about`, so the company column
 * points at contact, the same call aurora's footer made for the same reason.
 */
export const FOOTER_COLUMNS: readonly FooterColumn[] = [
  {
    key: 'hosting',
    titleKey: 'nova.footer.hosting',
    links: [
      { key: 'web', labelKey: 'nova.footer.webHosting', to: '/hosting' },
      { key: 'wordpress', labelKey: 'nova.footer.wordpressHosting', to: '/hosting' },
      { key: 'pricing', labelKey: 'nova.footer.pricing', to: '/hosting#plans' },
      { key: 'trial', labelKey: 'nova.footer.trial', to: '/products' },
    ],
  },
  {
    key: 'domains',
    titleKey: 'nova.footer.domains',
    links: [
      { key: 'register', labelKey: 'nova.footer.domainRegister', to: '/domains' },
      { key: 'transfer', labelKey: 'nova.footer.domainTransfer', to: '/domains/transfer' },
    ],
  },
  {
    key: 'products',
    titleKey: 'nova.footer.products',
    links: [
      { key: 'catalogue', labelKey: 'nova.footer.catalogue', to: '/products' },
      { key: 'email', labelKey: 'nova.footer.email', to: '/products' },
    ],
  },
  {
    key: 'company',
    titleKey: 'nova.footer.company',
    links: [
      { key: 'about', labelKey: 'nova.footer.about', to: '/contact' },
      { key: 'contact', labelKey: 'nova.footer.contact', to: '/contact' },
      { key: 'announcements', labelKey: 'nova.footer.announcements', to: '/announcements' },
    ],
  },
  {
    key: 'support',
    titleKey: 'nova.footer.support',
    links: [
      { key: 'help', labelKey: 'nova.footer.helpCentre', to: '/knowledgebase' },
      { key: 'faq', labelKey: 'nova.footer.faq', to: '/faq' },
      { key: 'login', labelKey: 'nova.footer.clientArea', to: '/client/login' },
    ],
  },
  {
    key: 'legal',
    titleKey: 'nova.footer.legal',
    links: [
      { key: 'terms', labelKey: 'nova.footer.terms', to: '/terms' },
      { key: 'privacy', labelKey: 'nova.footer.privacy', to: '/privacy' },
      { key: 'refund', labelKey: 'nova.footer.refund', to: '/refund-policy' },
      { key: 'aup', labelKey: 'nova.footer.aup', to: '/acceptable-use' },
    ],
  },
]

/**
 * Preferred order for the comparison table's rows.
 *
 * The rows themselves are whatever an operator entered under Admin → Products →
 * Specification, so this list cannot add one: it only says which order the
 * familiar lines read best in when they are present. Matching is
 * case-insensitive on the label, and anything unrecognised — a line in Armenian,
 * a line this list never anticipated — keeps its original position after the
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
  const rank = (label: string) => {
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
