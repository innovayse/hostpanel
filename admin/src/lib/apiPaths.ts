/**
 * Backend route roots used by more than one request. A request builds its URL from one of these
 * plus the member id or action, so a route is written once and renamed in one place.
 */

/** Root of each backend route family; the value is the path under `/api`. */
export enum ApiPath {
  /** `/admin/cancellation-requests` collection and its members. */
  AdminCancellationRequests = '/admin/cancellation-requests',
  /** `/admin/currencies` collection and its members. */
  AdminCurrencies = '/admin/currencies',
  /** `/admin/dashboard` collection and its members. */
  AdminDashboard = '/admin/dashboard',
  /** `/admin/email-logs` collection and its members. */
  AdminEmailLogs = '/admin/email-logs',
  /** `/admin/email-templates` collection and its members. */
  AdminEmailTemplates = '/admin/email-templates',
  /** `/admin/faqs` collection and its members. */
  AdminFaqs = '/admin/faqs',
  /** `/admin/integrations` collection and its members. */
  AdminIntegrations = '/admin/integrations',
  /** `/admin/knowledgebase` collection and its members. */
  AdminKnowledgebase = '/admin/knowledgebase',
  /** `/admin/knowledgebase/categories` collection and its members. */
  AdminKnowledgebaseCategories = '/admin/knowledgebase/categories',
  /** `/admin/locations` collection and its members. */
  AdminLocations = '/admin/locations',
  /** `/admin/migrations` collection and its members. */
  AdminMigrations = '/admin/migrations',
  /** `/admin/partners` collection and its members. */
  AdminPartners = '/admin/partners',
  /** `/admin/plugins` collection and its members. */
  AdminPlugins = '/admin/plugins',
  /** `/admin/projects` collection and its members. */
  AdminProjects = '/admin/projects',
  /** `/admin/server-groups` collection and its members. */
  AdminServerGroups = '/admin/server-groups',
  /** `/admin/servers` collection and its members. */
  AdminServers = '/admin/servers',
  /** `/admin/release-status` — installed version and latest GitHub release. */
  AdminReleaseStatus = '/admin/release-status',
  /** `/admin/settings` collection and its members. */
  AdminSettings = '/admin/settings',
  /** `/admin/slides` collection and its members. */
  AdminSlides = '/admin/slides',
  /** `/admin/testimonials` collection and its members. */
  AdminTestimonials = '/admin/testimonials',
  /** `/admin/tld-configs` collection and its members. */
  AdminTldConfigs = '/admin/tld-configs',
  /** `/admin/users` collection and its members. */
  AdminUsers = '/admin/users',
  /** `/announcements` collection and its members. */
  Announcements = '/announcements',
  /** `/auth` collection and its members. */
  Auth = '/auth',
  /** `/auth/logout` collection and its members. */
  AuthLogout = '/auth/logout',
  /** `/billing` collection and its members. */
  Billing = '/billing',
  /** `/billing/billable-items` collection and its members. */
  BillingBillableItems = '/billing/billable-items',
  /** `/billing/quotes` collection and its members. */
  BillingQuotes = '/billing/quotes',
  /** `/clients` collection and its members. */
  Clients = '/clients',
  /** `/domains` collection and its members. */
  Domains = '/domains',
  /** `/downloads` collection and its members. */
  Downloads = '/downloads',
  /** `/downloads/categories` collection and its members. */
  DownloadsCategories = '/downloads/categories',
  /** `/network-issues` collection and its members. */
  NetworkIssues = '/network-issues',
  /** `/orders` collection and its members. */
  Orders = '/orders',
  /** `/predefined-replies` collection and its members. */
  PredefinedReplies = '/predefined-replies',
  /** `/predefined-replies/categories` collection and its members. */
  PredefinedRepliesCategories = '/predefined-replies/categories',
  /** `/product-features` collection and its members. */
  ProductFeatures = '/product-features',
  /** `/products` collection and its members. */
  Products = '/products',
  /** `/provisioning` collection and its members. */
  Provisioning = '/provisioning',
  /** `/quotes` collection and its members. */
  Quotes = '/quotes',
  /** `/reports` collection and its members. */
  Reports = '/reports',
  /** `/services` collection and its members. */
  Services = '/services',
  /** `/tickets` collection and its members. */
  Tickets = '/tickets',
  /** `/tickets/client` collection and its members. */
  TicketsClient = '/tickets/client',
  /** `/tickets/departments` collection and its members. */
  TicketsDepartments = '/tickets/departments',
  /** `/transactions` collection and its members. */
  Transactions = '/transactions',
}

/** Prefix the same-origin proxy mounts the backend under; every request URL starts with it. */
export const API_BASE = '/api'

/**
 * Builds the endpoint path of one report under `/api/reports`. This is an API path, not a router
 * page: use `reportPagePath` for navigation.
 *
 * @param slug - Report slug, e.g. `invoices`.
 * @param suffix - Optional member under the report, e.g. `export`.
 * @returns `/reports/<slug>` or `/reports/<slug>/<suffix>`.
 */
export const reportApiPath = (slug: string, suffix?: string): string =>
  suffix ? `${ApiPath.Reports}/${slug}/${suffix}` : `${ApiPath.Reports}/${slug}`
