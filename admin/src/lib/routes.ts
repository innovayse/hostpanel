/**
 * Paths of the screens that more than one file links to, redirects to or highlights. A link names
 * its screen through this object, so renaming a route is one edit here and the router table.
 */

/** Closed set of screen paths shared across files. */
export enum AppRoute {
  /** The `/apps` root under which every plugin page lives (`/apps/<plugin>/<path>`). */
  Apps = '/apps',
  /** The `/billing` screen. */
  Billing = '/billing',
  /** The `/billing/add-transaction` screen. */
  BillingAddTransaction = '/billing/add-transaction',
  /** The `/billing/billable-items` screen. */
  BillingBillableItems = '/billing/billable-items',
  /** The `/billing/billable-items/add` screen. */
  BillingBillableItemsAdd = '/billing/billable-items/add',
  /** The `/billing/disputes` screen. */
  BillingDisputes = '/billing/disputes',
  /** The `/billing/gateway-log` screen. */
  BillingGatewayLog = '/billing/gateway-log',
  /** The `/billing/invoices` screen. */
  BillingInvoices = '/billing/invoices',
  /** The `/billing/offline-cc` screen. */
  BillingOfflineCc = '/billing/offline-cc',
  /** The `/billing/credit-notes` screen. */
  BillingCreditNotes = '/billing/credit-notes',
  /** The `/billing/bank-transfers` screen. */
  BillingBankTransfers = '/billing/bank-transfers',
  /** The `/billing/payment-claims` screen. */
  BillingPaymentClaims = '/billing/payment-claims',
  /** The `/billing/quotes` screen. */
  BillingQuotes = '/billing/quotes',
  /** The `/billing/quotes/add` screen. */
  BillingQuotesAdd = '/billing/quotes/add',
  /** The `/billing/renewals` screen. */
  BillingRenewals = '/billing/renewals',
  /** The `/billing/transactions` screen. */
  BillingTransactions = '/billing/transactions',
  /** The `/clients` screen. */
  Clients = '/clients',
  /** The `/clients/add` screen. */
  ClientsAdd = '/clients/add',
  /** The `/clients/addons` screen. */
  ClientsAddons = '/clients/addons',
  /** The `/clients/affiliates` screen. */
  ClientsAffiliates = '/clients/affiliates',
  /** The `/clients/cancellations` screen. */
  ClientsCancellations = '/clients/cancellations',
  /** The `/clients/domain-registrations` screen. */
  ClientsDomainRegistrations = '/clients/domain-registrations',
  /** The `/clients/shared-hostings` screen. */
  ClientsSharedHostings = '/clients/shared-hostings',
  /** The `/clients/users` screen. */
  ClientsUsers = '/clients/users',
  /** The `/dashboard` screen. */
  Dashboard = '/dashboard',
  /** The `/domains` screen. */
  Domains = '/domains',
  /** The `/integrations` screen. */
  Integrations = '/integrations',
  /** The `/integrations/payments` screen. */
  IntegrationsPayments = '/integrations/payments',
  /** The `/login` screen. */
  Login = '/login',
  /** The `/migration` screen. */
  Migration = '/migration',
  /** The `/notifications` screen. */
  Notifications = '/notifications',
  /** The `/orders` screen. */
  Orders = '/orders',
  /** The `/plugins` screen. */
  Plugins = '/plugins',
  /** The `/reports` screen. */
  Reports = '/reports',
  /** The `/reports/daily-performance` screen. */
  ReportsDailyPerformance = '/reports/daily-performance',
  /** The `/servers` screen. */
  Servers = '/servers',
  /** The `/services` screen. */
  Services = '/services',
  /** The `/settings` screen. */
  Settings = '/settings',
  /** The `/settings/currencies` screen. */
  SettingsCurrencies = '/settings/currencies',
  /** The `/settings/email-templates` screen. */
  SettingsEmailTemplates = '/settings/email-templates',
  /** The `/settings/products` screen. */
  SettingsProducts = '/settings/products',
  /** The `/settings/slides` screen. */
  SettingsSlides = '/settings/slides',
  /** The `/settings/tld-pricing` screen. */
  SettingsTldPricing = '/settings/tld-pricing',
  /** The `/setup` screen. */
  Setup = '/setup',
  /** The `/storefront` screen. */
  Storefront = '/storefront',
  /** The `/storefront/faqs` screen. */
  StorefrontFaqs = '/storefront/faqs',
  /** The `/storefront/locations` screen. */
  StorefrontLocations = '/storefront/locations',
  /** The `/storefront/partners` screen. */
  StorefrontPartners = '/storefront/partners',
  /** The `/storefront/projects` screen. */
  StorefrontProjects = '/storefront/projects',
  /** The `/storefront/testimonials` screen. */
  StorefrontTestimonials = '/storefront/testimonials',
  /** The `/support` screen. */
  Support = '/support',
  /** The `/support/announcements` screen. */
  SupportAnnouncements = '/support/announcements',
  /** The `/support/announcements/new` screen. */
  SupportAnnouncementsNew = '/support/announcements/new',
  /** The `/support/downloads` screen. */
  SupportDownloads = '/support/downloads',
  /** The `/support/knowledgebase` screen. */
  SupportKnowledgebase = '/support/knowledgebase',
  /** The `/support/network-issues` screen. */
  SupportNetworkIssues = '/support/network-issues',
  /** The `/support/network-issues/new` screen. */
  SupportNetworkIssuesNew = '/support/network-issues/new',
  /** The `/support/overview` screen. */
  SupportOverview = '/support/overview',
  /** The `/support/predefined-replies` screen. */
  SupportPredefinedReplies = '/support/predefined-replies',
  /** The `/support/tickets` screen. */
  SupportTickets = '/support/tickets',
  /** The `/support/tickets/new` screen. */
  SupportTicketsNew = '/support/tickets/new',
}

/** Segments under `/clients/:id/` that a client screen can be opened at. */
export enum ClientSection {
  /** The summary overview. */
  Summary = 'summary',
  /** The profile form. */
  Profile = 'profile',
  /** The client's services. */
  Services = 'services',
  /** The client's domains. */
  Domains = 'domains',
  /** The client's users. */
  Users = 'users',
  /** The client's contacts. */
  Contacts = 'contacts',
  /** The client's billable items. */
  BillableItems = 'billable-items',
  /** The client's invoices. */
  Invoices = 'invoices',
  /** The client's quotes. */
  Quotes = 'quotes',
  /** The client's transactions. */
  Transactions = 'transactions',
  /** The client's credit: balance, history and manual adjustments. */
  Credit = 'credit',
  /** The client's tickets. */
  Tickets = 'tickets',
  /** The client's email log. */
  Emails = 'emails',
  /** The client's activity log. */
  Log = 'log',
}

/** An entity id as it appears in a path. */
export type RouteId = number | string

/**
 * Path of the create screen under a list screen.
 *
 * @param base - The list screen path.
 * @returns `<base>/create`.
 */
export const createPath = (base: AppRoute): string => `${base}/create`

/**
 * Path of the edit screen of one entry under a list screen.
 *
 * @param base - The list screen path.
 * @param id - Entry ID.
 * @returns `<base>/<id>/edit`.
 */
export const editPath = (base: AppRoute, id: RouteId): string => `${base}/${id}/edit`

/**
 * Path of a client's summary page.
 *
 * @param clientId - Client ID.
 * @returns `/clients/<id>`.
 */
export const clientPath = (clientId: RouteId): string => `${AppRoute.Clients}/${clientId}`

/**
 * Path of one section of a client.
 *
 * @param clientId - Client ID.
 * @param section - The section segment.
 * @returns `/clients/<id>/<section>`.
 */
export const clientSectionPath = (clientId: RouteId, section: ClientSection): string => `${clientPath(clientId)}/${section}`

/**
 * Path of one item inside a client section (a service, a domain, a ticket).
 *
 * @param clientId - Client ID.
 * @param section - The section segment.
 * @param itemId - Item ID.
 * @returns `/clients/<id>/<section>/<itemId>`.
 */
export const clientItemPath = (clientId: RouteId, section: ClientSection, itemId: RouteId): string =>
  `${clientSectionPath(clientId, section)}/${itemId}`

/**
 * Path of a client's new-ticket screen.
 *
 * @param clientId - Client ID.
 * @returns `/clients/<id>/tickets/new`.
 */
export const clientTicketNewPath = (clientId: RouteId): string => `${clientSectionPath(clientId, ClientSection.Tickets)}/new`

/**
 * Path of an invoice (opened in its default mode).
 *
 * @param id - Invoice ID.
 * @returns `/billing/<id>`.
 */
export const invoicePath = (id: RouteId): string => `${AppRoute.Billing}/${id}`

/**
 * Path of a credit note's page.
 *
 * @param id - Credit note ID.
 * @returns `/billing/credit-notes/<id>`.
 */
export const creditNotePath = (id: RouteId): string => `${AppRoute.BillingCreditNotes}/${id}`

/**
 * Path of one plugin's details page.
 *
 * @param id - Plugin ID.
 * @returns `/plugins/<id>`.
 */
export const pluginPath = (id: RouteId): string => `${AppRoute.Plugins}/${id}`

/**
 * Path of an invoice's read-only page.
 *
 * @param id - Invoice ID.
 * @returns `/billing/invoices/<id>/view`.
 */
export const invoiceViewPath = (id: RouteId): string => `${AppRoute.BillingInvoices}/${id}/view`

/**
 * Path of an invoice's edit page.
 *
 * @param id - Invoice ID.
 * @returns `/billing/invoices/<id>/edit`.
 */
export const invoiceEditPath = (id: RouteId): string => `${AppRoute.BillingInvoices}/${id}/edit`

/**
 * Path of one quote.
 *
 * @param id - Quote ID.
 * @returns `/billing/quotes/<id>`.
 */
export const quotePath = (id: RouteId): string => `${AppRoute.BillingQuotes}/${id}`

/**
 * Path of the add-quote screen, optionally pre-filled for a client.
 *
 * @param clientId - Client to pre-fill, when known.
 * @returns `/billing/quotes/add`, with `?clientId=` when a client is given.
 */
export const quoteAddPath = (clientId?: RouteId): string =>
  clientId === undefined ? AppRoute.BillingQuotesAdd : `${AppRoute.BillingQuotesAdd}?clientId=${clientId}`

/**
 * Path of a transaction's edit page.
 *
 * @param id - Transaction ID.
 * @returns `/billing/transactions/<id>/edit`.
 */
export const transactionEditPath = (id: RouteId): string => `${AppRoute.BillingTransactions}/${id}/edit`

/**
 * Path of one order.
 *
 * @param id - Order ID.
 * @returns `/orders/<id>`.
 */
export const orderPath = (id: RouteId): string => `${AppRoute.Orders}/${id}`

/**
 * Path of one support ticket.
 *
 * @param id - Ticket ID.
 * @returns `/support/tickets/<id>`.
 */
export const supportTicketPath = (id: RouteId): string => `${AppRoute.SupportTickets}/${id}`

/**
 * Path of an integration category page.
 *
 * @param category - Category key.
 * @returns `/integrations/<category>`.
 */
export const integrationCategoryPath = (category: string): string => `${AppRoute.Integrations}/${category}`

/**
 * Path of an integration's configure page.
 *
 * @param slug - Integration slug.
 * @returns `/integrations/<slug>/configure`.
 */
export const integrationConfigurePath = (slug: string): string => `${AppRoute.Integrations}/${slug}/configure`

/**
 * Path of one report page.
 *
 * @param slug - Report slug.
 * @returns `/reports/<slug>`.
 */
export const reportPagePath = (slug: string): string => `${AppRoute.Reports}/${slug}`

/**
 * Path of a page a plugin adds to the admin panel.
 *
 * @param pluginId - Id of the plugin.
 * @param path - The page's filled path below the plugin's prefix, without a leading `/`; empty for its root page.
 * @returns `/apps/<plugin>`, or `/apps/<plugin>/<path>`.
 */
export const pluginPagePath = (pluginId: string, path: string): string =>
  path === '' ? `${AppRoute.Apps}/${encodeURIComponent(pluginId)}` : `${AppRoute.Apps}/${encodeURIComponent(pluginId)}/${path}`
