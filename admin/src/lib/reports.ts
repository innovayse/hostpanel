/**
 * Canonical registry of all reports.
 * Single source of truth for both the inner sidebar and the router children,
 * so the two never drift apart.
 */
import type { Component } from 'vue'
import { translate } from '@/lib/translate'

/** One report entry. */
export interface ReportDef {
  /** URL slug under /reports/. */
  slug: string
  /** Display label shown in the sidebar, read in the active language when asked. */
  readonly label: string
  /** Icon name (key into `REPORT_ICONS`). */
  icon: string
  /** Lazy-loaded view component. */
  component: () => Promise<Component>
}

/**
 * Builds one registry entry whose label follows the active language.
 *
 * @param slug - URL slug under /reports/.
 * @param labelKey - Locale key of the sidebar label.
 * @param icon - Icon name (key into `REPORT_ICONS`).
 * @param component - Lazy import of the view.
 * @returns The entry; `label` is looked up each time it is read, so a language switch applies.
 */
const defineReport = (slug: string, labelKey: string, icon: string, component: () => Promise<Component>): ReportDef => ({
  slug,
  get label() {
    return translate(labelKey)
  },
  icon,
  component,
})

/** All reports in display order. */
export const REPORTS: ReportDef[] = [
  defineReport('aging-invoices', 'reports.agingInvoices.agingInvoices', 'clock', () => import('@/views/AgingInvoicesView.vue')),
  defineReport('annual-income', 'reports.annualIncome.annualIncomeReport', 'bar-chart', () => import('@/views/AnnualIncomeView.vue')),
  defineReport('client-statement', 'reports.titles.reports.clientStatement', 'file-text', () => import('@/views/ClientStatementView.vue')),
  defineReport('clients', 'shared.clients', 'users', () => import('@/views/ClientsReportView.vue')),
  defineReport('clients-by-country', 'reports.clientsByCountry.clientsByCountry', 'globe', () => import('@/views/ClientsByCountryView.vue')),
  defineReport('credits-reviewer', 'reports.creditsReviewer.creditsReviewer', 'credit-card', () => import('@/views/CreditsReviewerView.vue')),
  defineReport('customer-retention', 'reports.titles.reports.customerRetentionTime', 'repeat', () => import('@/views/CustomerRetentionView.vue')),
  defineReport('daily-performance', 'reports.dailyPerformance.dailyPerformance', 'activity', () => import('@/views/DailyPerformanceView.vue')),
  defineReport('direct-debit', 'reports.directDebit.directDebitProcessing', 'dollar-sign', () => import('@/views/DirectDebitView.vue')),
  defineReport('disk-usage', 'reports.titles.reports.diskUsageSummary', 'hard-drive', () => import('@/views/DiskUsageView.vue')),
  defineReport('domain-renewal-emails', 'reports.titles.reports.domainRenewalEmails', 'mail', () => import('@/views/DomainRenewalEmailsView.vue')),
  defineReport('domains', 'shared.domains', 'link', () => import('@/views/DomainsReportView.vue')),
  defineReport('income-forecast', 'reports.incomeForecast.incomeForecast', 'trending-up', () => import('@/views/IncomeForecastView.vue')),
  defineReport('income-by-product', 'reports.titles.reports.incomeByProduct', 'package', () => import('@/views/IncomeByProductView.vue')),
  defineReport('invoices', 'shared.invoices', 'file', () => import('@/views/InvoicesReportView.vue')),
  defineReport('monthly-orders', 'reports.titles.reports.monthlyOrders', 'shopping-cart', () => import('@/views/MonthlyOrdersView.vue')),
  defineReport('monthly-transactions', 'reports.titles.reports.monthlyTransactions', 'shuffle', () => import('@/views/MonthlyTransactionsView.vue')),
  defineReport('new-customers', 'reports.newCustomers.newCustomers', 'user-plus', () => import('@/views/NewCustomersView.vue')),
  defineReport('oss', 'reports.titles.reports.oss', 'flag', () => import('@/views/OssReportView.vue')),
  defineReport('pdf-batch', 'reports.pdfBatch.pdfBatch', 'layers', () => import('@/views/PdfBatchView.vue')),
  defineReport('product-suspensions', 'reports.productSuspensions.productSuspensions', 'pause', () => import('@/views/ProductSuspensionsView.vue')),
  defineReport('promotions', 'reports.titles.reports.promotionsUsage', 'tag', () => import('@/views/PromotionsView.vue')),
  defineReport('sales-tax', 'reports.salesTax.salesTaxLiability', 'percent', () => import('@/views/SalesTaxView.vue')),
  defineReport('server-revenue-forecasts', 'reports.serverRevenueForecasts.serverRevenueForecasts', 'server', () => import('@/views/ServerRevenueForecastsView.vue')),
  defineReport('services', 'nav.servicesTitle', 'box', () => import('@/views/ServicesReportView.vue')),
  defineReport('smarty-compatibility', 'reports.smartyCompatibility.smartyCompatibility', 'check-circle', () => import('@/views/SmartyCompatibilityView.vue')),
  defineReport('ssl-monitoring', 'reports.sslMonitoring.sslCertificateMonitoring', 'lock', () => import('@/views/SslMonitoringView.vue')),
  defineReport('support-ticket-replies', 'reports.titles.reports.supportTicketReplies', 'message-square', () => import('@/views/SupportTicketsReportView.vue')),
  defineReport('ticket-feedback-comments', 'reports.ticketFeedbackComments.ticketFeedbackComments', 'message-circle', () => import('@/views/TicketFeedbackCommentsView.vue')),
  defineReport('ticket-feedback-scores', 'reports.ticketFeedbackScores.ticketFeedbackScores', 'star', () => import('@/views/TicketFeedbackScoresView.vue')),
  defineReport('ticket-ratings-reviewer', 'reports.titles.reports.ticketRatingsReviewer', 'award', () => import('@/views/TicketRatingsReviewerView.vue')),
  defineReport('ticket-tags', 'reports.titles.reports.ticketTags', 'hash', () => import('@/views/TicketTagsView.vue')),
  defineReport('top-clients', 'reports.topClients.top10ClientsByIncome', 'trophy', () => import('@/views/TopClientsView.vue')),
  defineReport('transactions', 'shared.transactions', 'list', () => import('@/views/TransactionsReportView.vue')),
]
