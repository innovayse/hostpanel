/** Route table and navigation guard of the admin SPA. */
import type { Component } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import { authMiddleware } from '@/middleware/auth'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { REPORTS } from '@/lib/reports'
import { AppRoute } from '@/lib/routes'
import { withRetry } from '@/lib/lazyRoute'
import { prefetchRoute } from '@/lib/prefetchRoute'
import { navigationAdmitted, navigationFailed, navigationSettled } from '@/middleware/navigation'

/** Every view and layout as a lazy loader, keyed by its path relative to this file; Vite splits each into its own chunk. */
const loaders = import.meta.glob<{ default: Component }>(['../views/*.vue', '../layouts/*.vue'])

/**
 * Looks up one routed file and makes it a lazy route component.
 *
 * Views and layouts alike are plain `() => import()` loaders, the lazy form vue-router supports; the
 * router awaits them before it switches screens, and the navigation store tells the shell about that
 * wait (see `middleware/navigation.ts`). Each loader retries a failed download, so one dropped request
 * does not leave the click with no effect.
 *
 * @param file - Path under `src/`, such as `views/LoginView.vue` or `layouts/AppLayout.vue`.
 * @returns The loader to put in a route record.
 * @throws When no such file exists, so a typo fails when the table is built rather than on navigation.
 */
const lazy = (file: string): (() => Promise<{ default: Component }>) => {
  const loader = loaders[`../${file}`]
  if (!loader) {
    throw new Error(`Unknown routed file: ${file}`)
  }
  return withRetry(loader)
}

/**
 * Sends a bare client URL to its summary tab.
 *
 * @param to - The requested location.
 * @param to.path - Its path.
 * @returns The same path with `/summary` appended.
 */
const clientSummaryRedirect = (to: { path: string }): { path: string } => ({ path: `${to.path.replace(/\/$/, '')}/summary` })

/**
 * Every navigation starts at the top of the page.
 *
 * @returns The scroll position to restore.
 */
const scrollToTop = (): { top: number } => ({ top: 0 })

/** The application router; every screen is a lazily loaded view under the authenticated layout. */
const router = createRouter({
  history: createWebHistory(getRuntimeConfig().basePath),
  scrollBehavior: scrollToTop,
  routes: [
    {
      // Not `public: true`. The guard judges this route on its own terms (setup still
      // outstanding, and a deployment that owns its own accounts) and the public rule
      // would bounce the operator to /dashboard the moment registration signed them in,
      // before they had claimed the Admin role.
      path: AppRoute.Setup,
      component: lazy('views/SetupView.vue'),
      meta: { setup: true },
    },
    {
      path: '/verify-email',
      component: lazy('views/VerifyEmailView.vue'),
      meta: { public: true },
    },
    {
      path: AppRoute.Login,
      component: lazy('views/LoginView.vue'),
      meta: { public: true },
    },
    // /auth/callback is gone with the browser-side code exchange: the SSO returns to
    // /api/auth/callback, which the API answers using the client secret.
    {
      path: '/',
      component: lazy('layouts/AppLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: AppRoute.Dashboard },
        { path: 'dashboard', component: lazy('views/DashboardView.vue') },
        { path: 'notifications', component: lazy('views/NotificationsView.vue') },
        { path: 'clients', component: lazy('views/ClientsListView.vue') },
        { path: 'clients/add', component: lazy('views/AddClientView.vue') },
        { path: 'clients/users', component: lazy('views/ManageUsersView.vue') },
        { path: 'clients/shared-hostings', component: lazy('views/ServicesListView.vue') },
        { path: 'clients/addons', component: lazy('views/ServiceAddonsView.vue') },
        { path: 'clients/domain-registrations', component: lazy('views/DomainRegistrationsView.vue') },
        { path: 'clients/cancellations', component: lazy('views/CancellationRequestsView.vue') },
        { path: 'clients/affiliates', component: lazy('views/ManageAffiliatesView.vue') },
        {
          path: 'clients/:id',
          component: lazy('layouts/ClientLayout.vue'),
          children: [
            { path: '', redirect: clientSummaryRedirect },
            { path: 'summary', component: lazy('views/ClientSummaryView.vue') },
            { path: 'profile', component: lazy('views/ClientProfileView.vue') },
            { path: 'services', component: lazy('views/ClientServicesListView.vue') },
            { path: 'services/:serviceId', component: lazy('views/ClientServiceDetailView.vue') },
            { path: 'users', component: lazy('views/ClientUsersView.vue') },
            { path: 'contacts', component: lazy('views/ClientContactsView.vue') },
            { path: 'billable-items', component: lazy('views/ClientBillableItemsView.vue') },
            { path: 'invoices', component: lazy('views/ClientInvoicesView.vue') },
            { path: 'quotes', component: lazy('views/ClientQuotesView.vue') },
            { path: 'transactions', component: lazy('views/ClientTransactionsView.vue') },
            { path: 'tickets', component: lazy('views/ClientTicketsView.vue') },
            { path: 'tickets/new', component: lazy('views/ClientOpenTicketView.vue') },
            { path: 'tickets/:ticketId', component: lazy('views/ClientTicketDetailView.vue') },
            { path: 'emails', component: lazy('views/ClientEmailsView.vue') },
            { path: 'log', component: lazy('views/ClientLogView.vue') },
            { path: 'domains', component: lazy('views/ClientDomainsListView.vue') },
            { path: 'domains/:domainId', component: lazy('views/ClientDomainDetailView.vue') },
          ],
        },
        {
          path: 'billing',
          children: [
            { path: '', redirect: 'invoices' },
            { path: 'invoices', component: lazy('views/InvoicesListView.vue') },
            { path: 'invoices/:id/:action', component: lazy('views/InvoiceDetailView.vue') },
            { path: ':id', component: lazy('views/InvoiceDetailView.vue') },
            { path: 'billable-items', component: lazy('views/BillableItemsView.vue') },
            { path: 'billable-items/add', component: lazy('views/AddBillableItemView.vue') },
            { path: 'quotes', component: lazy('views/QuotesListView.vue') },
            { path: 'quotes/add', component: lazy('views/AddQuoteView.vue') },
            { path: 'quotes/new', component: lazy('views/QuoteDetailView.vue') },
            { path: 'quotes/:id', component: lazy('views/QuoteDetailView.vue') },
            { path: 'offline-cc', component: lazy('views/OfflineCCView.vue') },
            { path: 'disputes', component: lazy('views/DisputesView.vue') },
            { path: 'gateway-log', component: lazy('views/GatewayLogView.vue') },
            { path: 'payment-claims', component: lazy('views/PaymentClaimsView.vue') },
            { path: 'transactions', component: lazy('views/TransactionsListView.vue') },
            { path: 'add-transaction', component: lazy('views/AddTransactionView.vue') },
            { path: 'transactions/:id/edit', component: lazy('views/EditTransactionView.vue') },
          ],
        },
        {
          path: 'reports',
          component: lazy('layouts/ReportsLayout.vue'),
          children: [
            { path: '', redirect: AppRoute.ReportsDailyPerformance },
            /** One route per catalogued report. */
            ...REPORTS.map(r => ({ path: r.slug, component: withRetry(r.component) })),
          ],
        },
        { path: 'orders', component: lazy('views/OrdersListView.vue') },
        { path: 'orders/:id', component: lazy('views/OrderDetailView.vue') },
        { path: 'services', component: lazy('views/ServicesListView.vue') },
        { path: 'domains', component: lazy('views/DomainsListView.vue') },
        { path: 'support', redirect: AppRoute.SupportOverview },
        { path: 'support/overview', component: lazy('views/SupportOverviewView.vue') },
        { path: 'support/tickets', component: lazy('views/SupportTicketsView.vue') },
        { path: 'support/tickets/new', component: lazy('views/SupportOpenTicketView.vue') },
        { path: 'support/tickets/:id', component: lazy('views/SupportTicketDetailView.vue') },
        { path: 'support/predefined-replies', component: lazy('views/PredefinedRepliesView.vue') },
        { path: 'support/announcements', component: lazy('views/AnnouncementsListView.vue') },
        { path: 'support/announcements/new', component: lazy('views/AnnouncementFormView.vue') },
        { path: 'support/announcements/:id/edit', component: lazy('views/AnnouncementFormView.vue') },
        { path: 'support/downloads', component: lazy('views/DownloadsView.vue') },
        { path: 'support/knowledgebase', component: lazy('views/KnowledgebaseView.vue') },
        { path: 'support/network-issues', component: lazy('views/NetworkIssuesView.vue') },
        { path: 'support/network-issues/new', component: lazy('views/NetworkIssueFormView.vue') },
        { path: 'support/network-issues/:id/edit', component: lazy('views/NetworkIssueFormView.vue') },
        { path: 'plugins', component: lazy('views/PluginsView.vue') },
        { path: 'servers', component: lazy('views/ServersView.vue') },
        { path: 'integrations/:slug/configure', component: lazy('views/IntegrationDetailView.vue') },
        { path: 'migration', component: lazy('views/MigrationView.vue') },
        {
          path: 'integrations',
          component: lazy('layouts/IntegrationsLayout.vue'),
          children: [
            { path: '', redirect: AppRoute.IntegrationsPayments },
            { path: ':category', component: lazy('views/IntegrationsView.vue') },
          ],
        },
        { path: 'settings', component: lazy('views/SystemSettingsView.vue') },
        { path: 'settings/email-templates', component: lazy('views/EmailTemplatesView.vue') },
        { path: 'settings/products', component: lazy('views/ProductsView.vue') },
        { path: 'settings/slides', component: lazy('views/SlidesView.vue') },
        { path: 'settings/slides/create', component: lazy('views/SlideFormView.vue') },
        { path: 'settings/slides/:id/edit', component: lazy('views/SlideFormView.vue') },
        { path: 'settings/tld-pricing', component: lazy('views/TldConfigsView.vue') },
        { path: 'settings/tld-pricing/create', component: lazy('views/TldConfigFormView.vue') },
        { path: 'settings/tld-pricing/:id/edit', component: lazy('views/TldConfigFormView.vue') },
        // The old gateways table read an endpoint that never existed; payment gateways are configured
        // under Integrations, so a bookmark to the old address goes there.
        { path: 'settings/gateways', redirect: AppRoute.IntegrationsPayments },
        { path: 'settings/currencies', component: lazy('views/CurrenciesView.vue') },
        { path: 'storefront/testimonials', component: lazy('views/TestimonialsView.vue') },
        { path: 'storefront/testimonials/create', component: lazy('views/TestimonialFormView.vue') },
        { path: 'storefront/testimonials/:id/edit', component: lazy('views/TestimonialFormView.vue') },
        { path: 'storefront/partners', component: lazy('views/PartnersView.vue') },
        { path: 'storefront/partners/create', component: lazy('views/PartnerFormView.vue') },
        { path: 'storefront/partners/:id/edit', component: lazy('views/PartnerFormView.vue') },
        { path: 'storefront/projects', component: lazy('views/ProjectsView.vue') },
        { path: 'storefront/projects/create', component: lazy('views/ProjectFormView.vue') },
        { path: 'storefront/projects/:id/edit', component: lazy('views/ProjectFormView.vue') },
        { path: 'storefront/faqs', component: lazy('views/FaqsView.vue') },
        { path: 'storefront/faqs/create', component: lazy('views/FaqFormView.vue') },
        { path: 'storefront/faqs/:id/edit', component: lazy('views/FaqFormView.vue') },
        { path: 'storefront/locations', component: lazy('views/LocationsView.vue') },
        { path: 'storefront/locations/create', component: lazy('views/LocationFormView.vue') },
        { path: 'storefront/locations/:id/edit', component: lazy('views/LocationFormView.vue') },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: AppRoute.Login },
  ],
})

// First: start downloading the target's chunks now. The session check below can take a few round trips
// on a cold page load, and vue-router would otherwise only begin fetching the screen after it finished.
router.beforeEach((to) => {
  prefetchRoute(to)
})

router.beforeEach(authMiddleware)

// Last: the navigation passed every guard, so the shell may now stand a skeleton in for the screen
// whose chunk is still on its way. The two hooks below end that state, or turn it into the load error.
router.beforeEach(navigationAdmitted)
router.afterEach(navigationSettled)
router.onError(navigationFailed)

export default router
