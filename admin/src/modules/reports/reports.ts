/**
 * Canonical registry of all reports (WHMCS 1:1).
 * Single source of truth for both the inner sidebar and the router children,
 * so the two never drift apart.
 */
import type { Component } from 'vue'

/** One report entry. */
export interface ReportDef {
  /** URL slug under /reports/. */
  slug: string
  /** Display label shown in the sidebar. */
  label: string
  /** Icon name (key into the sidebar ICONS map). */
  icon: string
  /** Lazy-loaded view component. */
  component: () => Promise<Component>
}

/** All 37 reports in WHMCS order. */
export const REPORTS: ReportDef[] = [
  { slug: 'affiliates-overview', label: 'Affiliates Overview', icon: 'share', component: () => import('./views/AffiliatesOverviewView.vue') },
  { slug: 'aging-invoices', label: 'Aging Invoices', icon: 'clock', component: () => import('./views/AgingInvoicesView.vue') },
  { slug: 'annual-income', label: 'Annual Income Report', icon: 'bar-chart', component: () => import('./views/AnnualIncomeView.vue') },
  { slug: 'client', label: 'Client', icon: 'user', component: () => import('./views/ClientReportView.vue') },
  { slug: 'client-sources', label: 'Client Sources', icon: 'pie-chart', component: () => import('./views/ClientSourcesView.vue') },
  { slug: 'client-statement', label: 'Client Statement', icon: 'file-text', component: () => import('./views/ClientStatementView.vue') },
  { slug: 'clients', label: 'Clients', icon: 'users', component: () => import('./views/ClientsReportView.vue') },
  { slug: 'clients-by-country', label: 'Clients by Country', icon: 'globe', component: () => import('./views/ClientsByCountryView.vue') },
  { slug: 'credits-reviewer', label: 'Credits Reviewer', icon: 'credit-card', component: () => import('./views/CreditsReviewerView.vue') },
  { slug: 'customer-retention', label: 'Customer Retention Time', icon: 'repeat', component: () => import('./views/CustomerRetentionView.vue') },
  { slug: 'daily-performance', label: 'Daily Performance', icon: 'activity', component: () => import('./views/DailyPerformanceView.vue') },
  { slug: 'direct-debit', label: 'Direct Debit Processing', icon: 'dollar-sign', component: () => import('./views/DirectDebitView.vue') },
  { slug: 'disk-usage', label: 'Disk Usage Summary', icon: 'hard-drive', component: () => import('./views/DiskUsageView.vue') },
  { slug: 'domain-renewal-emails', label: 'Domain Renewal Emails', icon: 'mail', component: () => import('./views/DomainRenewalEmailsView.vue') },
  { slug: 'domains', label: 'Domains', icon: 'link', component: () => import('./views/DomainsReportView.vue') },
  { slug: 'income-forecast', label: 'Income Forecast', icon: 'trending-up', component: () => import('./views/IncomeForecastView.vue') },
  { slug: 'income-by-product', label: 'Income by Product', icon: 'package', component: () => import('./views/IncomeByProductView.vue') },
  { slug: 'invoices', label: 'Invoices', icon: 'file', component: () => import('./views/InvoicesReportView.vue') },
  { slug: 'monthly-orders', label: 'Monthly Orders', icon: 'shopping-cart', component: () => import('./views/MonthlyOrdersView.vue') },
  { slug: 'monthly-transactions', label: 'Monthly Transactions', icon: 'shuffle', component: () => import('./views/MonthlyTransactionsView.vue') },
  { slug: 'new-customers', label: 'New Customers', icon: 'user-plus', component: () => import('./views/NewCustomersView.vue') },
  { slug: 'pdf-batch', label: 'Pdf Batch', icon: 'layers', component: () => import('./views/PdfBatchView.vue') },
  { slug: 'product-suspensions', label: 'Product Suspensions', icon: 'pause', component: () => import('./views/ProductSuspensionsView.vue') },
  { slug: 'promotions', label: 'Promotions Usage', icon: 'tag', component: () => import('./views/PromotionsView.vue') },
  { slug: 'sales-tax', label: 'Sales Tax Liability', icon: 'percent', component: () => import('./views/SalesTaxView.vue') },
  { slug: 'server-revenue-forecasts', label: 'Server Revenue Forecasts', icon: 'server', component: () => import('./views/ServerRevenueForecastsView.vue') },
  { slug: 'services', label: 'Services', icon: 'box', component: () => import('./views/ServicesReportView.vue') },
  { slug: 'smarty-compatibility', label: 'Smarty Compatibility', icon: 'check-circle', component: () => import('./views/SmartyCompatibilityView.vue') },
  { slug: 'ssl-monitoring', label: 'Ssl Certificate Monitoring', icon: 'lock', component: () => import('./views/SslMonitoringView.vue') },
  { slug: 'support-ticket-replies', label: 'Support Ticket Replies', icon: 'message-square', component: () => import('./views/SupportTicketsReportView.vue') },
  { slug: 'ticket-feedback-comments', label: 'Ticket Feedback Comments', icon: 'message-circle', component: () => import('./views/TicketFeedbackCommentsView.vue') },
  { slug: 'ticket-feedback-scores', label: 'Ticket Feedback Scores', icon: 'star', component: () => import('./views/TicketFeedbackScoresView.vue') },
  { slug: 'ticket-ratings-reviewer', label: 'Ticket Ratings Reviewer', icon: 'award', component: () => import('./views/TicketRatingsReviewerView.vue') },
  { slug: 'ticket-tags', label: 'Ticket Tags', icon: 'hash', component: () => import('./views/TicketTagsView.vue') },
  { slug: 'top-clients', label: 'Top 10 Clients by Income', icon: 'trophy', component: () => import('./views/TopClientsView.vue') },
  { slug: 'transactions', label: 'Transactions', icon: 'list', component: () => import('./views/TransactionsReportView.vue') },
  { slug: 'vat-moss', label: 'Vat Moss', icon: 'flag', component: () => import('./views/VatMossView.vue') },
]
