/** Stats shown on the admin dashboard. */
export interface DashboardStats {
  /** Total lifetime revenue. */
  totalRevenue: number
  /** Revenue for the current month. */
  monthlyRevenue: number
  /** Number of active services. */
  activeServices: number
  /** Number of overdue invoices. */
  overdueInvoices: number
  /** Number of open support tickets. */
  openTickets: number
  /** Total registered clients. */
  totalClients: number
}

/** Summary DTO returned by the clients list endpoint. */
export interface ClientListItem {
  /** Unique client identifier. */
  id: number
  /** Identity user ID. */
  userId: string
  /** Email address from the linked Identity user. */
  email: string
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Account status (Active, Inactive, Suspended, Closed). */
  status: string
  /** True if the linked Identity user has been deleted. */
  isUserDeleted: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Full client DTO returned by the detail endpoint. */
export interface ClientDetail extends ClientListItem {
  /** Phone number. */
  phone?: string
  /** Street address. */
  street?: string
  /** Second address line. */
  address2?: string
  /** City. */
  city?: string
  /** State or province. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** ISO 4217 currency code. */
  currency?: string
  /** Payment method identifier or label. */
  paymentMethod?: string
  /** Billing contact name or reference. */
  billingContact?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** Whether the client receives general notification emails. */
  notifyGeneral: boolean
  /** Whether the client receives invoice notification emails. */
  notifyInvoice: boolean
  /** Whether the client receives support notification emails. */
  notifySupport: boolean
  /** Whether the client receives product notification emails. */
  notifyProduct: boolean
  /** Whether the client receives domain notification emails. */
  notifyDomain: boolean
  /** Whether the client receives affiliate notification emails. */
  notifyAffiliate: boolean
  /** Whether late fees are applied to overdue invoices. */
  lateFees: boolean
  /** Whether overdue notices are sent for this client. */
  overdueNotices: boolean
  /** Whether the client is exempt from taxes. */
  taxExempt: boolean
  /** Whether invoices are generated separately per product. */
  separateInvoices: boolean
  /** Whether credit card processing is disabled for this client. */
  disableCcProcessing: boolean
  /** Whether the client has opted in to marketing emails. */
  marketingOptIn: boolean
  /** Whether the client status updates are tracked. */
  statusUpdate: boolean
  /** Whether the client may use single sign-on. */
  allowSso: boolean
  /** Associated contacts. */
  contacts: Contact[]
}

/** Country option returned by the reference/countries endpoint. */
export interface CountryOption {
  /** ISO 3166-1 alpha-2 country code. */
  code: string
  /** Human-readable country name. */
  name: string
}

/** Currency option returned by the reference/currencies endpoint. */
export interface CurrencyOption {
  /** ISO 4217 currency code (e.g. "USD"). */
  code: string
  /** Human-readable currency name. */
  name: string
  /** Currency symbol (e.g. "$"). */
  symbol: string
}

/** Payload for creating a new client via the admin API. */
export interface AdminCreateClientPayload {
  /** True to create a new Identity user alongside the client. */
  createNewUser: boolean
  /** Identity user ID to associate with, when createNewUser is false. */
  existingUserId?: string
  /** Email for the new user (required when createNewUser is true). */
  email?: string
  /** Password for the new user (required when createNewUser is true). */
  password?: string
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Optional phone number. */
  phone?: string
  /** Street address line 1. */
  street?: string
  /** Street address line 2. */
  address2?: string
  /** City. */
  city?: string
  /** State or province. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** ISO 4217 currency code. */
  currency?: string
  /** Preferred language code. */
  language?: string
  /** Payment method identifier. */
  paymentMethod?: string
  /** Billing contact name or reference. */
  billingContact?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** Client status (Active, Inactive, Suspended, Closed). */
  status?: string
  /** Whether the client receives general notification emails. */
  notifyGeneral: boolean
  /** Whether the client receives invoice notification emails. */
  notifyInvoice: boolean
  /** Whether the client receives support notification emails. */
  notifySupport: boolean
  /** Whether the client receives product notification emails. */
  notifyProduct: boolean
  /** Whether the client receives domain notification emails. */
  notifyDomain: boolean
  /** Whether the client receives affiliate notification emails. */
  notifyAffiliate: boolean
  /** Whether late fees are applied to overdue invoices. */
  lateFees: boolean
  /** Whether overdue notices are sent. */
  overdueNotices: boolean
  /** Whether the client is exempt from taxes. */
  taxExempt: boolean
  /** Whether invoices are generated separately per product. */
  separateInvoices: boolean
  /** Whether credit card processing is disabled. */
  disableCcProcessing: boolean
  /** Whether the client has opted in to marketing emails. */
  marketingOptIn: boolean
  /** Whether the client status updates are tracked. */
  statusUpdate: boolean
  /** Whether the client may use single sign-on. */
  allowSso: boolean
}

/** Contact record linked to a client account. */
export interface Contact {
  /** Contact primary key. */
  id: number
  /** First name. */
  firstName: string
  /** Last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Email address. */
  email: string
  /** Phone number. */
  phone?: string
  /** Contact type: Billing, Technical, or General. */
  type: string
  /** Street address. */
  street?: string
  /** Second address line. */
  address2?: string
  /** City. */
  city?: string
  /** State or region. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** General email notifications. */
  notifyGeneral: boolean
  /** Invoice email notifications. */
  notifyInvoice: boolean
  /** Support email notifications. */
  notifySupport: boolean
  /** Product email notifications. */
  notifyProduct: boolean
  /** Domain email notifications. */
  notifyDomain: boolean
  /** Affiliate email notifications. */
  notifyAffiliate: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Single line item on an invoice. */
export interface InvoiceItem {
  /** Unique line item identifier. */
  id: number
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Number of units. */
  quantity: number
  /** Line total (unitPrice x quantity). */
  amount: number
}

/** Payment, refund, or credit transaction recorded against an invoice. */
export interface InvoiceTransaction {
  /** Unique transaction identifier. */
  id: number
  /** Transaction date (ISO 8601). */
  date: string
  /** Payment gateway used. */
  gateway: string
  /** Gateway transaction reference. */
  transactionId: string
  /** Transaction amount. */
  amount: number
  /** Transaction fees charged by the gateway. */
  fees: number
  /** Transaction type: Payment, Refund, or Credit. */
  type: string
  /** Optional admin notes. */
  notes?: string
}

/** Represents an invoice. */
export interface Invoice {
  /** Unique invoice identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Invoice status (Draft, Unpaid, Paid, Overdue, Cancelled, Refunded, Collections, PaymentPending). */
  status: string
  /** ISO 8601 due date. */
  dueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** Total amount due. */
  total: number
  /** Optional admin notes. */
  notes?: string
  /** ISO 8601 invoice date. */
  invoiceDate: string
  /** Payment method label. */
  paymentMethod?: string
  /** Tax rate percentage. */
  taxRate: number
  /** Computed tax amount. */
  tax: number
  /** Sub-total before tax and credit. */
  subTotal: number
  /** Credit applied to the invoice. */
  credit: number
  /** ISO 8601 payment timestamp, if paid. */
  paidAt?: string
  /** Gateway transaction reference, if paid. */
  gatewayTransactionId?: string
  /** Display name of the owning client. */
  clientName?: string
  /** Line items on the invoice. */
  items: InvoiceItem[]
  /** Payment/refund/credit transactions. */
  transactions: InvoiceTransaction[]
}

/** Represents a single admin activity log entry for a client. */
export interface ActivityLog {
  /** Unique identifier. */
  id: number
  /** Human-readable description of the action performed. */
  description: string
  /** Display name of the admin who performed the action. */
  adminName: string | null
  /** Email of the admin who performed the action. */
  adminEmail: string | null
  /** IP address from which the action originated. */
  ipAddress: string | null
  /** UTC ISO timestamp of the action. */
  createdAt: string
}

/** Represents a single email log entry. */
export interface EmailLog {
  /** Unique identifier. */
  id: number
  /** Recipient email address. */
  to: string
  /** Email subject line. */
  subject: string
  /** UTC timestamp of the send attempt. */
  sentAt: string
  /** Whether the email was delivered successfully. */
  success: boolean
  /** Error message if delivery failed; null on success. */
  error: string | null
}

/** DTO representing a support ticket reply. */
export interface TicketReply {
  /** Reply primary key. */
  id: number
  /** Reply message body. */
  message: string
  /** Display name of the reply author. */
  authorName: string
  /** Whether this reply was posted by a staff member. */
  isStaffReply: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Full DTO representing a single support ticket with all its replies. */
export interface Ticket {
  /** Unique ticket identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Ticket subject line. */
  subject: string
  /** Initial message body. */
  message: string
  /** Current lifecycle status (Open, AwaitingReply, Answered, Closed). */
  status: string
  /** Priority level (Low, Medium, High). */
  priority: string
  /** Department FK, if assigned. */
  departmentId: number | null
  /** Department name, if assigned. */
  departmentName: string | null
  /** Assigned staff member FK, if any. */
  assignedToStaffId: number | null
  /** Assigned staff member name, if any. */
  assignedToStaffName: string | null
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** All replies on this ticket. */
  replies: TicketReply[]
}

/** Summary DTO for ticket list views. */
export interface TicketListItem {
  /** Ticket primary key. */
  id: number
  /** Ticket subject line. */
  subject: string
  /** Current lifecycle status as a string. */
  status: string
  /** Priority level as a string. */
  priority: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** Total number of replies. */
  replyCount: number
  /** Department name, if assigned. */
  departmentName: string | null
  /** ISO 8601 timestamp of the most recent reply, if any. */
  lastReplyAt: string | null
  /** Whether this ticket has been flagged by staff. */
  isFlagged: boolean
  /** FK to the client who opened the ticket. */
  clientId: number
}

/** Support overview dashboard statistics. */
export interface SupportOverviewStats {
  /** Number of new tickets in the period. */
  newTickets: number
  /** Number of client replies in the period. */
  clientReplies: number
  /** Number of staff replies in the period. */
  staffReplies: number
  /** Number of tickets with no staff reply in the period. */
  ticketsWithoutReply: number
  /** Average time to first staff response, or null when no data. */
  averageFirstResponse: string | null
  /** Array of 24 integers representing ticket counts per hour (0-23). */
  ticketsByHour: number[]
}

/** Ticket statistics for a client, broken down by time period. */
export interface ClientTicketStats {
  /** Tickets opened in the current calendar month. */
  openedThisMonth: number
  /** Tickets opened in the previous calendar month. */
  openedLastMonth: number
  /** Tickets opened in the current calendar year. */
  openedThisYear: number
  /** Tickets opened in the previous calendar year. */
  openedLastYear: number
}

/** DTO representing a support department. */
export interface Department {
  /** Department primary key. */
  id: number
  /** Department name. */
  name: string
  /** Department email address. */
  email: string
}

/** DTO representing an announcement. */
export interface Announcement {
  /** Announcement primary key. */
  id: number
  /** Announcement title. */
  title: string
  /** HTML content body. */
  content: string
  /** Whether the announcement is published. */
  isPublished: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** DTO representing a network issue. */
export interface NetworkIssue {
  /** Network issue primary key. */
  id: number
  /** Issue title. */
  title: string
  /** Issue type (Server, Other). */
  type: string
  /** Server name, if applicable. */
  server: string | null
  /** Priority level (Low, Medium, High, Critical). */
  priority: string
  /** Lifecycle status (Reported, Investigating, Scheduled, Resolved). */
  status: string
  /** ISO 8601 start date. */
  startDate: string
  /** ISO 8601 end date, if any. */
  endDate: string | null
  /** HTML description body. */
  description: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** DTO representing a predefined reply category. */
export interface PredefinedReplyCategory {
  /** Category primary key. */
  id: number
  /** Category display name. */
  name: string
  /** Parent category ID, or null if top-level. */
  parentCategoryId: number | null
  /** Number of replies in this category. */
  replyCount: number
}

/** DTO representing a predefined reply. */
export interface PredefinedReply {
  /** Reply primary key. */
  id: number
  /** Reply display name. */
  name: string
  /** Reply content body. */
  content: string
  /** FK to the category. */
  categoryId: number
  /** Category name, if resolved. */
  categoryName: string | null
}

/** DTO representing a knowledge base category. */
export interface KbCategory {
  /** Category primary key. */
  id: number
  /** Category display name. */
  name: string
  /** Category description. */
  description: string
  /** Whether hidden from clients. */
  isHidden: boolean
  /** Parent category ID, or null if top-level. */
  parentCategoryId: number | null
  /** Number of articles in this category. */
  articleCount: number
}

/** DTO representing a knowledge base article. */
export interface KbArticle {
  /** Article primary key. */
  id: number
  /** Article title. */
  title: string
  /** Article body content. */
  content: string
  /** Category name. */
  category: string
  /** Whether published and visible to clients. */
  isPublished: boolean
}

/** Represents a domain registration. */
export interface Domain {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Domain name (e.g. example.com). */
  name: string
  /** Domain status (active, expired, pending). */
  status: string
  /** ISO 8601 expiry date. */
  expiresAt: string
}

/** DTO for domain registration list with client name and pricing. */
export interface DomainRegistration {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Domain name (e.g. example.com). */
  name: string
  /** Registration period label (e.g. "1 Year"). */
  regPeriod: string
  /** Name of the registrar module. */
  registrar: string | null
  /** Recurring renewal amount. */
  recurringAmount: number
  /** ISO 4217 currency code for the price. */
  priceCurrency: string
  /** Next renewal payment due date (ISO 8601). */
  nextDueDate: string
  /** Domain expiration date (ISO 8601). */
  expiresAt: string
  /** Current lifecycle status. */
  status: string
  /** Whether auto-renew is enabled. */
  autoRenew: boolean
}

/** Full domain detail returned by GET /api/domains/{id}. */
export interface DomainDetail {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Fully-qualified domain name. */
  name: string
  /** Top-level domain suffix. */
  tld: string
  /** Current lifecycle status. */
  status: string
  /** ISO 8601 registration date. */
  registeredAt: string
  /** ISO 8601 expiry date. */
  expiresAt: string
  /** Whether auto-renew is enabled. */
  autoRenew: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Whether registrar transfer lock is enabled. */
  isLocked: boolean
  /** Registrar-assigned reference. */
  registrarRef: string | null
  /** EPP authorization code. */
  eppCode: string | null
  /** Linked hosting service ID. */
  linkedServiceId: number | null
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring renewal amount. */
  recurringAmount: number
  /** ISO 4217 currency code. */
  priceCurrency: string
  /** ISO 8601 next due date. */
  nextDueDate: string
  /** Registrar module name. */
  registrar: string | null
  /** Registration period in years. */
  registrationPeriod: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion code. */
  promotionCode: string | null
  /** External subscription reference. */
  subscriptionId: string | null
  /** Free-text admin notes. */
  adminNotes: string | null
  /** Order ID that created this domain. */
  orderId: number | null
  /** Order type: "Register" or "Transfer". */
  orderType: string
  /** Whether DNS management is enabled. */
  dnsManagement: boolean
  /** Whether email forwarding is enabled. */
  emailForwarding: boolean
  /** Nameserver list. */
  nameservers: NameserverItem[]
  /** DNS records. */
  dnsRecords: DnsRecordItem[]
  /** Email forwarding rules. */
  emailForwardingRules: EmailForwardingRuleItem[]
  /** Domain reminder history. */
  reminders: DomainReminderItem[]
}

/** Nameserver DTO. */
export interface NameserverItem {
  /** Nameserver primary key. */
  id: number
  /** Nameserver hostname. */
  host: string
}

/** DNS record DTO. */
export interface DnsRecordItem {
  /** Record primary key. */
  id: number
  /** Record type (A, AAAA, CNAME, MX, TXT, NS, SRV). */
  type: string
  /** Record host/name. */
  host: string
  /** Record value. */
  value: string
  /** Time-to-live in seconds. */
  ttl: number
  /** Priority (MX/SRV only). */
  priority: number | null
}

/** Email forwarding rule DTO. */
export interface EmailForwardingRuleItem {
  /** Rule primary key. */
  id: number
  /** Source alias. */
  source: string
  /** Destination email address. */
  destination: string
  /** Whether the rule is active. */
  isActive: boolean
}

/** Domain reminder history DTO. */
export interface DomainReminderItem {
  /** Reminder primary key. */
  id: number
  /** Reminder type label. */
  reminderType: string
  /** Recipient email. */
  sentTo: string
  /** ISO 8601 sent timestamp. */
  sentAt: string
}

/** Represents a system setting key-value pair. */
export interface Setting {
  /** Unique setting identifier. */
  id: number
  /** Setting key. */
  key: string
  /** Setting value. */
  value: string
  /** Optional human-readable description. */
  description?: string
}

/** Represents an email template. */
export interface EmailTemplate {
  /** Unique template identifier. */
  id: number
  /** Template name/slug. */
  name: string
  /** Email subject line. */
  subject: string
  /** HTML body content. */
  body: string
}

/** Represents a hosted service (hosting, VPS, etc.). */
export interface Service {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Product name (e.g. Shared Hosting, VPS). */
  productName: string
  /** Primary domain assigned to the service. */
  domain: string
  /** Service status (active, suspended, cancelled, pending). */
  status: string
  /** ISO 8601 next due date for renewal. */
  nextDueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Enriched service DTO for admin service list views. */
export interface ServiceListItem {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Product display name. */
  productName: string
  /** Linked domain name, or null if no domain is linked. */
  domain: string | null
  /** Resolved price based on billing cycle. */
  price: number
  /** ISO 4217 currency code (e.g. "USD"). */
  priceCurrency: string
  /** Billing cycle: "monthly" or "annual". */
  billingCycle: string
  /** Current lifecycle status (Active, Pending, Suspended, Terminated). */
  status: string
  /** ISO 8601 next renewal date, or null if not yet active. */
  nextDueDate: string | null
}

/** Represents a product/plan available for purchase. */
export interface Product {
  /** Unique product identifier. */
  id: number
  /** Product name. */
  name: string
  /** Product type (hosting, vps, domain, etc.). */
  type: string
  /** Price per billing cycle. */
  price: number
  /** Billing cycle (monthly, annual, etc.). */
  billingCycle: string
  /** Whether the product is publicly visible. */
  isActive: boolean
}

/**
 * Represents a payment gateway configuration.
 * @deprecated Use IntegrationDto from the integrations module instead.
 */
export interface Gateway {
  /** Unique gateway identifier. */
  id: number
  /** Gateway name (e.g. Stripe, PayPal). */
  name: string
  /** Whether this gateway is currently enabled. */
  isEnabled: boolean
  /** Display order in checkout. */
  displayOrder: number
}

/** DTO for a cancellation request in the admin list. */
export interface CancellationRequestItem {
  /** Unique identifier. */
  id: number
  /** ID of the service being cancelled. */
  serviceId: number
  /** Name of the product/service. */
  serviceName: string
  /** ID of the owning client. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Cancellation type (e.g. "Immediate", "End of Billing Period"). */
  type: string
  /** Reason provided by the client. */
  reason: string | null
  /** Request status ("Open" or "Closed"). */
  status: string
  /** ISO 8601 creation date. */
  createdAt: string
}

/** Full service detail returned by GET /api/services/{id}. */
export interface ServiceDetail {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Product identifier. */
  productId: number
  /** Product display name. */
  productName: string
  /** Linked domain name, if any. */
  domain?: string
  /** Dedicated IP address, if assigned. */
  dedicatedIp?: string
  /** Control panel or service username. */
  username?: string
  /** Control panel or service password. */
  password?: string
  /** Quantity of units purchased. */
  quantity: number
  /** Amount charged for the first payment. */
  firstPaymentAmount: number
  /** Recurring charge per billing cycle. */
  recurringAmount: number
  /** Payment method identifier or label. */
  paymentMethod?: string
  /** Applied promotion code, if any. */
  promotionCode?: string
  /** External subscription ID (e.g. Stripe subscription). */
  subscriptionId?: string
  /** Billing cycle: "monthly" or "annual". */
  billingCycle: string
  /** Current lifecycle status (Active, Pending, Suspended, Terminated). */
  status: string
  /** External provisioning reference (e.g. cPanel account ID). */
  provisioningRef?: string
  /** ISO 8601 next renewal date, if applicable. */
  nextDueDate?: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** ISO 8601 termination timestamp, if terminated. */
  terminatedAt?: string
  /** Whether automatic suspension is overridden. */
  overrideAutoSuspend: boolean
  /** ISO 8601 date until which the service is suspended. */
  suspendUntil?: string
  /** Whether the service auto-terminates at end of billing cycle. */
  autoTerminateEndOfCycle: boolean
  /** Reason for auto-termination, if applicable. */
  autoTerminateReason?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** FK to the assigned server, if any. */
  serverId?: number
  /** Display name of the assigned server. */
  serverName?: string
}

/** Generic paginated response wrapper. */
export interface PagedResult<T> {
  /** Data items for the current page. */
  items: T[]
  /** Total number of items across all pages. */
  totalCount: number
  /** Current page number (1-based). */
  page: number
  /** Number of items per page. */
  pageSize: number
}

/** Granular permissions for users linked to a client account. */
export enum ClientPermission {
  /** No permissions. */
  None = 0,
  /** Modify the master account profile. */
  ModifyMasterProfile = 1,
  /** View and manage contacts. */
  ViewManageContacts = 2,
  /** View products and services. */
  ViewProductsServices = 4,
  /** View and modify product passwords. */
  ViewModifyPasswords = 8,
  /** Allow single sign-on to hosting panels. */
  AllowSingleSignOn = 16,
  /** View domains. */
  ViewDomains = 32,
  /** Manage domain settings. */
  ManageDomainSettings = 64,
  /** View and pay invoices. */
  ViewPayInvoices = 128,
  /** View and accept quotes. */
  ViewAcceptQuotes = 256,
  /** View and open support tickets. */
  ViewOpenSupportTickets = 512,
  /** View and manage affiliate account. */
  ViewManageAffiliate = 1024,
  /** View emails. */
  ViewEmails = 2048,
  /** Place new orders, upgrades, and cancellations. */
  PlaceNewOrders = 4096,
  /** All permissions combined. */
  All = 8191,
}

/** User linked to a client account with permissions. */
export interface ClientUserItem {
  /** Identity user ID. */
  userId: string
  /** First name. */
  firstName: string
  /** Last name. */
  lastName: string
  /** Email address. */
  email: string
  /** True if this user is the account owner. */
  isOwner: boolean
  /** Granted permissions as bit-flags integer. */
  permissions: number
  /** Last login timestamp or null. */
  lastLoginAt: string | null
  /** When the user was linked. */
  createdAt: string
}

/** Represents a billable item that can be invoiced to a client. */
export interface BillableItem {
  /** Unique billable item identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** FK to client service, if applicable. */
  serviceId?: number
  /** Product/service name from the linked service. */
  serviceName?: string
  /** Charge description. */
  description: string
  /** Total charge amount. */
  amount: number
  /** Hours or quantity value. */
  hoursQty: number
  /** Whether hoursQty represents hours (true) or quantity (false). */
  isHours: boolean
  /** How and when this item should be invoiced. */
  invoiceAction: string
  /** ISO 8601 due date. */
  dueDate: string
  /** FK to invoice if invoiced, null if uninvoiced. */
  invoiceId?: number
  /** Number of times this item has been invoiced. */
  invoiceCount: number
  /** Recurrence interval (recur every N periods). */
  recurrenceInterval?: number
  /** Recurrence period unit. */
  recurrencePeriod?: string
  /** Max number of recurrences (null = unlimited). */
  recurrenceLimit?: number
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Response from the billable items list endpoint. */
export interface BillableItemsResult {
  /** All uninvoiced items for the client. */
  uninvoicedItems: BillableItem[]
  /** Sum of all uninvoiced item amounts. */
  uninvoicedTotal: number
  /** Paginated invoiced items. */
  invoicedItems: PagedResult<BillableItem>
}

/** Quote stage values. */
export type QuoteStage = 'Draft' | 'Delivered' | 'OnHold' | 'Accepted' | 'Lost' | 'Dead'

/** Quote list item returned by the list endpoint. */
export interface QuoteListItem {
  /** Unique quote identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Display name of the owning client. */
  clientName: string
  /** Quote subject line. */
  subject: string
  /** ISO 8601 creation date. */
  dateCreated: string
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Total quoted amount. */
  total: number
  /** Current stage of the quote. */
  stage: QuoteStage
}

/** Quote line item. */
export interface QuoteItem {
  /** Unique line item identifier. */
  id: number
  /** Number of units. */
  quantity: number
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Discount percentage (0-100). */
  discountPercent: number
  /** Whether the item is subject to tax. */
  taxed: boolean
  /** Computed line total. */
  amount: number
}

/** Full quote detail returned by GET /api/quotes/{id}. */
export interface Quote {
  /** Unique quote identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Quote subject line. */
  subject: string
  /** Current stage of the quote. */
  stage: QuoteStage
  /** ISO 8601 creation date. */
  dateCreated: string
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Sub-total before tax. */
  subTotal: number
  /** Total quoted amount. */
  total: number
  /** Proposal/sales text sent to the client. */
  proposalText?: string
  /** Notes visible to the customer. */
  customerNotes?: string
  /** Internal admin-only notes. */
  adminNotes?: string
  /** Line items on the quote. */
  items: QuoteItem[]
}

/** Client-level financial ledger entry. */
export interface Transaction {
  /** Unique transaction identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** ISO 8601 transaction date. */
  date: string
  /** Human-readable description of the transaction. */
  description: string
  /** External gateway transaction reference. */
  transactionId: string
  /** Optional linked invoice identifier. */
  invoiceId: number | null
  /** Payment method used (e.g. Stripe, Manual). */
  paymentMethod: string
  /** Amount received. */
  amountIn: number
  /** Amount sent out. */
  amountOut: number
  /** Gateway fees. */
  fees: number
  /** Whether this transaction affected client credit balance. */
  addedToCredit: boolean
}

/** Response from the transactions list endpoint with summary totals. */
export interface TransactionsResult {
  /** Paginated transaction list. */
  transactions: PagedResult<Transaction>
  /** Sum of all AmountIn values. */
  totalIn: number
  /** Sum of all AmountOut values. */
  totalOut: number
  /** Sum of all Fees values. */
  totalFees: number
  /** Calculated balance: TotalIn - TotalOut - TotalFees. */
  balance: number
}

/** Invoice count and total for a single status. */
export interface InvoiceStatusCount {
  /** Number of invoices in this status. */
  count: number
  /** Sum of totals for invoices in this status. */
  total: number
}

/** Aggregated summary data for the client profile dashboard. */
export interface ClientSummaryData {
  /** Draft invoice stats. */
  draft: InvoiceStatusCount
  /** Unpaid invoice stats. */
  unpaid: InvoiceStatusCount
  /** Paid invoice stats. */
  paid: InvoiceStatusCount
  /** Overdue invoice stats. */
  overdue: InvoiceStatusCount
  /** Cancelled invoice stats. */
  cancelled: InvoiceStatusCount
  /** Refunded invoice stats. */
  refunded: InvoiceStatusCount
  /** Total income from transactions. */
  grossRevenue: number
  /** Total outgoing from transactions. */
  clientExpenses: number
  /** Total transaction fees. */
  totalFees: number
  /** Computed: grossRevenue - clientExpenses - totalFees. */
  netIncome: number
  /** Current credit balance. */
  creditBalance: number
  /** Services with Active status. */
  activeServicesCount: number
  /** Total number of services. */
  totalServicesCount: number
  /** Total number of domains. */
  totalDomainsCount: number
  /** Quotes with Accepted stage. */
  acceptedQuotesCount: number
  /** Total number of quotes. */
  totalQuotesCount: number
  /** Open tickets. */
  openTicketsCount: number
  /** Total tickets. */
  totalTicketsCount: number
  /** Last 5 email log entries. */
  recentEmails: EmailLog[]
}

/** DTO for order list items in admin view. */
export interface OrderListItem {
  /** Unique order identifier. */
  id: number
  /** Human-readable order number (e.g. "ORD-0001"). */
  orderNumber: string
  /** FK to the owning client. */
  clientId: number
  /** Client display name. */
  clientName: string
  /** Current order status. */
  status: 'Pending' | 'Active' | 'Cancelled' | 'Fraud'
  /** Payment gateway module name. */
  paymentMethod: string
  /** Total order amount. */
  total: number
  /** Linked invoice ID, if any. */
  invoiceId: number | null
  /** Number of items in the order. */
  itemCount: number
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** DTO for full order details in admin view. */
export interface OrderDetail extends OrderListItem {
  /** Invoice status if invoice exists. */
  invoiceStatus: string | null
  /** Client IP address at checkout. */
  ipAddress: string | null
  /** Admin notes. */
  notes: string | null
  /** Order line items. */
  items: OrderItemDetail[]
}

/** DTO for an individual order item. */
export interface OrderItemDetail {
  /** Item ID. */
  id: number
  /** Product ID. */
  productId: number
  /** Product name snapshot at order time. */
  productName: string
  /** Billing cycle. */
  billingCycle: string
  /** Domain name if applicable. */
  domain: string | null
  /** Hostname if applicable. */
  hostname: string | null
  /** First payment amount. */
  firstPaymentAmount: number
  /** Recurring amount. */
  recurringAmount: number
  /** Item status. */
  status: 'Pending' | 'Active' | 'Cancelled'
}

/** Labels for each permission flag, used for checkbox rendering. */
export const PERMISSION_LABELS: { flag: ClientPermission; label: string }[] = [
  { flag: ClientPermission.ModifyMasterProfile, label: 'Modify Master Account Profile' },
  { flag: ClientPermission.ViewManageContacts, label: 'View & Manage Contacts' },
  { flag: ClientPermission.ViewProductsServices, label: 'View Products & Services' },
  { flag: ClientPermission.ViewModifyPasswords, label: 'View & Modify Product Passwords' },
  { flag: ClientPermission.AllowSingleSignOn, label: 'Allow Single Sign-On' },
  { flag: ClientPermission.ViewDomains, label: 'View Domains' },
  { flag: ClientPermission.ManageDomainSettings, label: 'Manage Domain Settings' },
  { flag: ClientPermission.ViewPayInvoices, label: 'View & Pay Invoices' },
  { flag: ClientPermission.ViewAcceptQuotes, label: 'View & Accept Quotes' },
  { flag: ClientPermission.ViewOpenSupportTickets, label: 'View & Open Support Tickets' },
  { flag: ClientPermission.ViewManageAffiliate, label: 'View & Manage Affiliate Account' },
  { flag: ClientPermission.ViewEmails, label: 'View Emails' },
  { flag: ClientPermission.PlaceNewOrders, label: 'Place New Orders/Upgrades/Cancellations' },
]
