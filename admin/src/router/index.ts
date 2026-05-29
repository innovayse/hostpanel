import { createRouter, createWebHistory } from 'vue-router'
import { authMiddleware } from '../middleware/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  scrollBehavior: () => ({ top: 0 }),
  routes: [
    {
      path: '/setup',
      component: () => import('../modules/auth/views/SetupView.vue'),
      meta: { public: true, setup: true },
    },
    {
      path: '/verify-email',
      component: () => import('../modules/auth/views/VerifyEmailView.vue'),
      meta: { public: true },
    },
    {
      path: '/login',
      component: () => import('../modules/auth/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('../components/layout/AppLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/dashboard' },
        { path: 'dashboard', component: () => import('../modules/dashboard/views/DashboardView.vue') },
        { path: 'clients', component: () => import('../modules/clients/views/ClientsListView.vue') },
        { path: 'clients/add', component: () => import('../modules/clients/views/AddClientView.vue') },
        { path: 'clients/users', component: () => import('../modules/clients/views/ManageUsersView.vue') },
        { path: 'clients/shared-hostings', component: () => import('../modules/services/views/ServicesListView.vue') },
        { path: 'clients/addons', component: () => import('../modules/clients/views/ServiceAddonsView.vue') },
        { path: 'clients/domain-registrations', component: () => import('../modules/clients/views/DomainRegistrationsView.vue') },
        { path: 'clients/cancellations', component: () => import('../modules/clients/views/CancellationRequestsView.vue') },
        { path: 'clients/affiliates', component: () => import('../modules/clients/views/ManageAffiliatesView.vue') },
        {
          path: 'clients/:id',
          component: () => import('../modules/clients/views/ClientLayout.vue'),
          children: [
            { path: '', redirect: to => ({ path: `${to.path.replace(/\/$/, '')}/profile` }) },
            { path: 'profile', component: () => import('../modules/clients/views/ClientProfileView.vue') },
            { path: 'services', component: () => import('../modules/clients/views/ClientServicesListView.vue') },
            { path: 'services/:serviceId', component: () => import('../modules/clients/views/ClientServiceDetailView.vue') },
            { path: 'users', component: () => import('../modules/clients/views/ClientUsersView.vue') },
            { path: 'contacts', component: () => import('../modules/clients/views/ClientContactsView.vue') },
            { path: 'billable-items', component: () => import('../modules/clients/views/ClientBillableItemsView.vue') },
            { path: 'invoices', component: () => import('../modules/clients/views/ClientInvoicesView.vue') },
            { path: 'quotes', component: () => import('../modules/clients/views/ClientQuotesView.vue') },
            { path: 'transactions', component: () => import('../modules/clients/views/ClientTransactionsView.vue') },
            { path: 'domains', component: () => import('../modules/clients/views/ClientDomainsListView.vue') },
            { path: 'domains/:domainId', component: () => import('../modules/clients/views/ClientDomainDetailView.vue') },
          ],
        },
        {
          path: 'billing',
          children: [
            { path: '', redirect: 'invoices' },
            { path: 'invoices', component: () => import('../modules/billing/views/InvoicesListView.vue') },
            { path: 'invoices/:id/:action', component: () => import('../modules/billing/views/InvoiceDetailView.vue') },
            { path: ':id', component: () => import('../modules/billing/views/InvoiceDetailView.vue') },
            { path: 'billable-items', component: () => import('../modules/billing/views/BillableItemsView.vue') },
            { path: 'billable-items/add', component: () => import('../modules/billing/views/AddBillableItemView.vue') },
            { path: 'quotes', component: () => import('../modules/billing/views/QuotesListView.vue') },
            { path: 'quotes/add', component: () => import('../modules/billing/views/AddQuoteView.vue') },
            { path: 'offline-cc', component: () => import('../modules/billing/views/OfflineCCView.vue') },
            { path: 'disputes', component: () => import('../modules/billing/views/DisputesView.vue') },
            { path: 'gateway-log', component: () => import('../modules/billing/views/GatewayLogView.vue') },
          ],
        },
        { path: 'quotes/new', component: () => import('../modules/billing/views/QuoteDetailView.vue') },
        { path: 'quotes/:id', component: () => import('../modules/billing/views/QuoteDetailView.vue') },
        { path: 'services', component: () => import('../modules/services/views/ServicesListView.vue') },
        { path: 'domains', component: () => import('../modules/domains/views/DomainsListView.vue') },
        { path: 'support', component: () => import('../modules/support/views/TicketsListView.vue') },
        { path: 'support/:id', component: () => import('../modules/support/views/TicketDetailsView.vue') },
        { path: 'plugins', component: () => import('../modules/plugins/views/PluginsView.vue') },
        { path: 'servers', component: () => import('../modules/servers/views/ServersView.vue') },
        { path: 'integrations', component: () => import('../modules/integrations/views/IntegrationsView.vue') },
        { path: 'integrations/:slug', component: () => import('../modules/integrations/views/IntegrationDetailView.vue') },
        { path: 'settings', component: () => import('../modules/settings/views/SystemSettingsView.vue') },
        { path: 'settings/email-templates', component: () => import('../modules/settings/views/EmailTemplatesView.vue') },
        { path: 'settings/products', component: () => import('../modules/settings/views/ProductsView.vue') },
        { path: 'settings/gateways', component: () => import('../modules/settings/views/GatewaysView.vue') },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/login' },
  ],
})

router.beforeEach(authMiddleware)

export default router
