/**
 * Canonical registry of all reports.
 * Single source of truth for the inner sidebar, the router children and the layout's skeleton,
 * so they never drift apart. It also holds the fill scale of the reports world map.
 */
import { translate } from '@/lib/translate'

/** The groups the reports menu is divided into, in display order. */
export enum ReportGroup {
  /** The headline figures: daily, annual, forecast, by product. */
  Overview = 'overview',
  /** Invoices, transactions, tax and payment processing. */
  Finance = 'finance',
  /** Client lists, growth, retention and statements. */
  Clients = 'clients',
  /** Orders, services, suspensions and promotions. */
  OrdersServices = 'ordersServices',
  /** Domains, their renewals and certificates. */
  Domains = 'domains',
  /** Tickets, tags and feedback. */
  Support = 'support',
  /** Disk use and server revenue. */
  Servers = 'servers',
}

/** Locale key of the heading of each group of the reports menu. */
const REPORT_GROUP_LABEL_KEYS: Record<ReportGroup, string> = {
  [ReportGroup.Overview]: 'reports.groups.overview',
  [ReportGroup.Finance]: 'reports.groups.finance',
  [ReportGroup.Clients]: 'reports.groups.clients',
  [ReportGroup.OrdersServices]: 'reports.groups.ordersServices',
  [ReportGroup.Domains]: 'reports.groups.domains',
  [ReportGroup.Support]: 'reports.groups.support',
  [ReportGroup.Servers]: 'reports.groups.servers',
}

/** One report entry. */
export interface ReportDef {
  /** URL slug under /reports/. */
  slug: string
  /** Full title of the report, read in the active language when asked; the report page's own heading. */
  readonly label: string
  /** Short caption for the reports menu, read in the active language when asked. */
  readonly menuLabel: string
  /** The menu group the report is listed under. */
  group: ReportGroup
  /** Icon name (key into `REPORT_ICONS`). */
  icon: string
  /** Locale key of the short menu caption; the layout skeleton draws it before the translated label is read. */
  readonly menuKey: string
  /** The view's file under `src/`; the router loads it lazily and looks its skeleton up by it. */
  file: string
}

/** What `defineReport` needs to build an entry. */
interface ReportSpec {
  /** URL slug under /reports/. */
  slug: string
  /** Locale key of the full title. */
  titleKey: string
  /** Locale key of the short menu caption. */
  menuKey: string
  /** The menu group of the report. */
  group: ReportGroup
  /** Icon name (key into `REPORT_ICONS`). */
  icon: string
  /** The view's file under `src/`. */
  file: string
}

/**
 * Builds one registry entry whose labels follow the active language.
 *
 * @param spec - Slug, locale keys, group, icon and view file of the report.
 * @returns The entry; `label` and `menuLabel` are looked up each time they are read, so a language switch applies.
 */
const defineReport = (spec: ReportSpec): ReportDef => ({
  slug: spec.slug,
  get label() {
    return translate(spec.titleKey)
  },
  get menuLabel() {
    return translate(spec.menuKey)
  },
  group: spec.group,
  icon: spec.icon,
  menuKey: spec.menuKey,
  file: spec.file,
})

/**
 * The heading of a menu group in the active language.
 *
 * @param group - The group.
 * @returns Its translated heading.
 */
export const reportGroupLabel = (group: ReportGroup): string => translate(REPORT_GROUP_LABEL_KEYS[group])

/**
 * Keeps the reports whose short caption, full title or slug contains the text, ignoring case and surrounding spaces.
 *
 * @param reports - The reports to search, in menu order.
 * @param query - What the operator typed; blank keeps every report.
 * @returns The matching reports, in the same order.
 */
export const filterReports = (reports: readonly ReportDef[], query: string): ReportDef[] => {
  const needle = query.trim().toLowerCase()
  if (needle === '') {
    return [...reports]
  }
  return reports.filter(report => [report.menuLabel, report.label, report.slug].some(text => text.toLowerCase().includes(needle)))
}

/** All reports in menu order: by group, and inside a group by importance. */
export const REPORTS: ReportDef[] = [
  defineReport({ slug: 'daily-performance', titleKey: 'reports.dailyPerformance.dailyPerformance', menuKey: 'reports.menu.dailyPerformance', group: ReportGroup.Overview, icon: 'activity', file: 'views/DailyPerformanceView.vue' }),
  defineReport({ slug: 'annual-income', titleKey: 'reports.annualIncome.annualIncomeReport', menuKey: 'reports.menu.annualIncome', group: ReportGroup.Overview, icon: 'bar-chart', file: 'views/AnnualIncomeView.vue' }),
  defineReport({ slug: 'income-forecast', titleKey: 'reports.incomeForecast.incomeForecast', menuKey: 'reports.menu.incomeForecast', group: ReportGroup.Overview, icon: 'trending-up', file: 'views/IncomeForecastView.vue' }),
  defineReport({ slug: 'income-by-product', titleKey: 'reports.titles.reports.incomeByProduct', menuKey: 'reports.menu.incomeByProduct', group: ReportGroup.Overview, icon: 'package', file: 'views/IncomeByProductView.vue' }),
  defineReport({ slug: 'invoices', titleKey: 'shared.invoices', menuKey: 'reports.menu.invoices', group: ReportGroup.Finance, icon: 'file', file: 'views/InvoicesReportView.vue' }),
  defineReport({ slug: 'aging-invoices', titleKey: 'reports.agingInvoices.agingInvoices', menuKey: 'reports.menu.agingInvoices', group: ReportGroup.Finance, icon: 'clock', file: 'views/AgingInvoicesView.vue' }),
  defineReport({ slug: 'monthly-transactions', titleKey: 'reports.titles.reports.monthlyTransactions', menuKey: 'reports.menu.monthlyTransactions', group: ReportGroup.Finance, icon: 'shuffle', file: 'views/MonthlyTransactionsView.vue' }),
  defineReport({ slug: 'transactions', titleKey: 'shared.transactions', menuKey: 'reports.menu.transactions', group: ReportGroup.Finance, icon: 'list', file: 'views/TransactionsReportView.vue' }),
  defineReport({ slug: 'sales-tax', titleKey: 'reports.salesTax.salesTaxLiability', menuKey: 'reports.menu.salesTax', group: ReportGroup.Finance, icon: 'percent', file: 'views/SalesTaxView.vue' }),
  defineReport({ slug: 'oss', titleKey: 'reports.titles.reports.oss', menuKey: 'reports.menu.oss', group: ReportGroup.Finance, icon: 'flag', file: 'views/OssReportView.vue' }),
  defineReport({ slug: 'direct-debit', titleKey: 'reports.directDebit.directDebitProcessing', menuKey: 'reports.menu.directDebit', group: ReportGroup.Finance, icon: 'dollar-sign', file: 'views/DirectDebitView.vue' }),
  defineReport({ slug: 'credits-reviewer', titleKey: 'reports.creditsReviewer.creditsReviewer', menuKey: 'reports.menu.creditsReviewer', group: ReportGroup.Finance, icon: 'credit-card', file: 'views/CreditsReviewerView.vue' }),
  defineReport({ slug: 'clients', titleKey: 'shared.clients', menuKey: 'reports.menu.clients', group: ReportGroup.Clients, icon: 'users', file: 'views/ClientsReportView.vue' }),
  defineReport({ slug: 'new-customers', titleKey: 'reports.newCustomers.newCustomers', menuKey: 'reports.menu.newCustomers', group: ReportGroup.Clients, icon: 'user-plus', file: 'views/NewCustomersView.vue' }),
  defineReport({ slug: 'top-clients', titleKey: 'reports.topClients.top10ClientsByIncome', menuKey: 'reports.menu.topClients', group: ReportGroup.Clients, icon: 'trophy', file: 'views/TopClientsView.vue' }),
  defineReport({ slug: 'clients-by-country', titleKey: 'reports.clientsByCountry.clientsByCountry', menuKey: 'reports.menu.clientsByCountry', group: ReportGroup.Clients, icon: 'globe', file: 'views/ClientsByCountryView.vue' }),
  defineReport({ slug: 'customer-retention', titleKey: 'reports.titles.reports.customerRetentionTime', menuKey: 'reports.menu.customerRetention', group: ReportGroup.Clients, icon: 'repeat', file: 'views/CustomerRetentionView.vue' }),
  defineReport({ slug: 'client-statement', titleKey: 'reports.titles.reports.clientStatement', menuKey: 'reports.menu.clientStatement', group: ReportGroup.Clients, icon: 'file-text', file: 'views/ClientStatementView.vue' }),
  defineReport({ slug: 'monthly-orders', titleKey: 'reports.titles.reports.monthlyOrders', menuKey: 'reports.menu.monthlyOrders', group: ReportGroup.OrdersServices, icon: 'shopping-cart', file: 'views/MonthlyOrdersView.vue' }),
  defineReport({ slug: 'services', titleKey: 'nav.servicesTitle', menuKey: 'reports.menu.services', group: ReportGroup.OrdersServices, icon: 'box', file: 'views/ServicesReportView.vue' }),
  defineReport({ slug: 'product-suspensions', titleKey: 'reports.productSuspensions.productSuspensions', menuKey: 'reports.menu.productSuspensions', group: ReportGroup.OrdersServices, icon: 'pause', file: 'views/ProductSuspensionsView.vue' }),
  defineReport({ slug: 'promotions', titleKey: 'reports.titles.reports.promotionsUsage', menuKey: 'reports.menu.promotions', group: ReportGroup.OrdersServices, icon: 'tag', file: 'views/PromotionsView.vue' }),
  defineReport({ slug: 'domains', titleKey: 'shared.domains', menuKey: 'reports.menu.domains', group: ReportGroup.Domains, icon: 'link', file: 'views/DomainsReportView.vue' }),
  defineReport({ slug: 'domain-renewal-emails', titleKey: 'reports.titles.reports.domainRenewalEmails', menuKey: 'reports.menu.domainRenewalEmails', group: ReportGroup.Domains, icon: 'mail', file: 'views/DomainRenewalEmailsView.vue' }),
  defineReport({ slug: 'ssl-monitoring', titleKey: 'reports.sslMonitoring.sslCertificateMonitoring', menuKey: 'reports.menu.sslMonitoring', group: ReportGroup.Domains, icon: 'lock', file: 'views/SslMonitoringView.vue' }),
  defineReport({ slug: 'support-ticket-replies', titleKey: 'reports.titles.reports.supportTicketReplies', menuKey: 'reports.menu.supportTicketReplies', group: ReportGroup.Support, icon: 'message-square', file: 'views/SupportTicketsReportView.vue' }),
  defineReport({ slug: 'ticket-tags', titleKey: 'reports.titles.reports.ticketTags', menuKey: 'reports.menu.ticketTags', group: ReportGroup.Support, icon: 'hash', file: 'views/TicketTagsView.vue' }),
  defineReport({ slug: 'ticket-feedback-scores', titleKey: 'reports.ticketFeedbackScores.ticketFeedbackScores', menuKey: 'reports.menu.ticketFeedbackScores', group: ReportGroup.Support, icon: 'star', file: 'views/TicketFeedbackScoresView.vue' }),
  defineReport({ slug: 'ticket-feedback-comments', titleKey: 'reports.ticketFeedbackComments.ticketFeedbackComments', menuKey: 'reports.menu.ticketFeedbackComments', group: ReportGroup.Support, icon: 'message-circle', file: 'views/TicketFeedbackCommentsView.vue' }),
  defineReport({ slug: 'ticket-ratings-reviewer', titleKey: 'reports.titles.reports.ticketRatingsReviewer', menuKey: 'reports.menu.ticketRatingsReviewer', group: ReportGroup.Support, icon: 'award', file: 'views/TicketRatingsReviewerView.vue' }),
  defineReport({ slug: 'disk-usage', titleKey: 'reports.titles.reports.diskUsageSummary', menuKey: 'reports.menu.diskUsage', group: ReportGroup.Servers, icon: 'hard-drive', file: 'views/DiskUsageView.vue' }),
  defineReport({ slug: 'server-revenue-forecasts', titleKey: 'reports.serverRevenueForecasts.serverRevenueForecasts', menuKey: 'reports.menu.serverRevenueForecasts', group: ReportGroup.Servers, icon: 'server', file: 'views/ServerRevenueForecastsView.vue' }),
]

/* Fill scale of the reports world map: which tone a country gets for its value. */

/** One step of the scale: the class that fills a country and the class of its legend swatch. */
export interface MapFillStep {
  /** Tailwind fill class for a country on the map. */
  fill: string
  /** Tailwind background class for the same step in the legend. */
  swatch: string
}

/** Fill of a country that has no data. */
export const MAP_NO_DATA_FILL = 'fill-border'

/** Background of the "no data" legend swatch. */
export const MAP_NO_DATA_SWATCH = 'bg-border'

/** Fill of the country under the pointer or focus. */
export const MAP_ACTIVE_FILL = 'fill-primary-300'

/**
 * Steps from the lightest to the darkest tone. The primary token is the same in both themes, so the
 * opacity ramp reads against the light and the dark card alike. Classes are literals so Tailwind sees them.
 */
export const MAP_FILL_STEPS: readonly MapFillStep[] = [
  { fill: 'fill-primary-500/25', swatch: 'bg-primary-500/25' },
  { fill: 'fill-primary-500/40', swatch: 'bg-primary-500/40' },
  { fill: 'fill-primary-500/55', swatch: 'bg-primary-500/55' },
  { fill: 'fill-primary-500/75', swatch: 'bg-primary-500/75' },
  { fill: 'fill-primary-500', swatch: 'bg-primary-500' },
]

/**
 * Picks the step of the scale for a value, linear from just above zero up to the maximum.
 *
 * @param value - The value of the country; zero or less has no step.
 * @param max - The largest value on the map.
 * @returns The step index, or -1 when the value has none.
 */
export const mapFillIndex = (value: number, max: number): number => {
  if (value <= 0 || max <= 0) {
    return -1
  }
  const last = MAP_FILL_STEPS.length - 1
  return Math.min(last, Math.ceil((value / max) * MAP_FILL_STEPS.length) - 1)
}

/**
 * Fill class of a country for its value.
 *
 * @param value - The value of the country, or undefined when it has no data.
 * @param max - The largest value on the map.
 * @returns The Tailwind fill class; the neutral one without data.
 */
export const mapFillClass = (value: number | undefined, max: number): string => {
  const step = value === undefined ? undefined : MAP_FILL_STEPS[mapFillIndex(value, max)]
  return step?.fill ?? MAP_NO_DATA_FILL
}
