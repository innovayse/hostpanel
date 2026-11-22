/**
 * A registered domain and everything that hangs off it: nameservers, DNS records,
 * forwarding rules and renewal reminders.
 */


/** Represents a domain registration. */
export interface Domain {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Domain name (e.g. example.com). */
  name: string
  /** Domain lifecycle status. */
  status: DomainStatus
  /** ISO 8601 expiry date. */
  expiresAt: string
}

/** DTO for domain registration list with client name and pricing. */
export interface DomainRegistration {
  /** Unique domain identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client; every domain has one, since its client cannot be deleted. */
  clientName: string
  /** Domain name (e.g. example.com). */
  name: string
  /** Registration period in years; 0 or less means unknown. */
  regPeriodYears: number
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
  status: DomainStatus
  /** Whether auto-renew is enabled. */
  autoRenew: boolean
}

/** Registration, expiry and renewal dates of a domain and the length of its registration. */
export interface DomainTerm {
  /** ISO 8601 registration date. */
  registeredAt: string
  /** ISO 8601 expiry date. */
  expiresAt: string
  /** ISO 8601 next renewal payment due date. */
  nextDueDate: string
  /** Registration period in years. */
  registrationPeriod: number
}

/** The registrar a domain is registered with and the identifiers it holds for the domain. */
export interface DomainRegistrar {
  /** Registrar module name, or null when not set. */
  module: string | null
  /** Registrar-assigned reference, or null when not yet registered. */
  reference: string | null
  /** EPP authorization code, or null when not available. */
  eppCode: string | null
}

/** The optional operations the registrar that holds a domain offers; the screen shows an action only when its flag is on. */
export interface DomainCapabilities {
  /** Whether the registrar can switch DNS management on and off. */
  dnsManagement: boolean
  /** Whether the registrar can forward the domain's mail. */
  emailForwarding: boolean
  /** Whether the registrar answers a WHOIS lookup. */
  whois: boolean
}

/** The switches that can be turned on or off for a domain. */
export interface DomainFeatures {
  /** Whether auto-renew is enabled. */
  autoRenew: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Whether registrar transfer lock is enabled. */
  isLocked: boolean
  /** Whether DNS management is enabled. */
  dnsManagement: boolean
  /** Whether email forwarding is enabled. */
  emailForwarding: boolean
}

/** What a domain costs and how it is paid for. */
export interface DomainBilling {
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring renewal amount. */
  recurringAmount: number
  /** ISO 4217 currency code. */
  priceCurrency: string
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion code. */
  promotionCode: string | null
  /** External subscription reference. */
  subscriptionId: string | null
}

/** The order that created a domain and the service it is linked to. */
export interface DomainOrigin {
  /** Order ID that created this domain. */
  orderId: number | null
  /** Order type: "Register" or "Transfer". */
  orderType: string
  /** Linked hosting service ID. */
  linkedServiceId: number | null
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
  status: DomainStatus
  /** Free-text admin notes. */
  adminNotes: string | null
  /** Registration, expiry and renewal dates and the registration period. */
  term: DomainTerm
  /** The registrar and the identifiers it holds for the domain. */
  registrar: DomainRegistrar
  /** The auto-renew, privacy, lock, DNS and forwarding switches. */
  features: DomainFeatures
  /** Prices, currency, payment method, promotion and subscription. */
  billing: DomainBilling
  /** The order that created the domain and the service it is linked to. */
  origin: DomainOrigin
  /** Nameserver list. */
  nameservers: NameserverItem[]
  /** DNS records. */
  dnsRecords: DnsRecordItem[]
  /** Email forwarding rules. */
  emailForwardingRules: EmailForwardingRuleItem[]
  /** Domain reminder history. */
  reminders: DomainReminderItem[]
  /** What the registrar that holds the domain offers. */
  capabilities: DomainCapabilities
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
  /** Kind of reminder that was sent. */
  reminderType: DomainReminderType
  /** Recipient email. */
  sentTo: string
  /** ISO 8601 sent timestamp. */
  sentAt: string
}

/** What narrows the domain list: the search text and the filters of the filter dialog; each member left out means no narrowing. */
export interface DomainListFilters {
  /** Name search term; empty for none. */
  search?: string
  /** Keep only the domains in this lifecycle status. */
  status?: DomainStatus
  /** Keep only the domains managed through the registrar plugin with this id (any letter case). */
  registrarModule?: string
}

/** Filter and paging of the domain list. */
export interface DomainFilter extends DomainListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
  /** Keep only this client's domains. */
  clientId?: number | string
}

/** Body of a domain save: the editable fields of the detail page. */
export interface UpdateDomainPayload {
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring renewal amount. */
  recurringAmount: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion code. */
  promotionCode: string | null
  /** External subscription reference. */
  subscriptionId: string | null
  /** Free-text admin notes. */
  adminNotes: string | null
  /** ISO 8601 expiry date. */
  expiresAt: string | null
  /** ISO 8601 next due date. */
  nextDueDate: string | null
  /** Registration period in years. */
  registrationPeriod: number
  /** Lifecycle status. */
  status: DomainStatus
  /** Nameserver hostnames, empty ones dropped. */
  nameservers: string[]
}

/** WHOIS registrant contact fields, keyed by the field name the API expects. */
export type DomainContactPayload = Record<string, string>

/** Body that adds or replaces a DNS record. */
export interface DnsRecordPayload {
  /** Record type (A, MX and so on). */
  type: string
  /** Host label. */
  host: string
  /** Record value. */
  value: string
  /** Time-to-live in seconds. */
  ttl: number
  /** Priority (MX/SRV only), null otherwise. */
  priority: number | null
}

/** Body that adds or replaces an email forwarding rule. */
export interface ForwardingRulePayload {
  /** Source alias. */
  source: string
  /** Destination email address. */
  destination: string
  /** Whether the rule is active; omitted to keep the current state. */
  isActive?: boolean
}

/** A boolean switch of a domain feature (auto-renew, lock, privacy, DNS, forwarding). */
export interface DomainToggle {
  /** Whether the feature is turned on. */
  enabled: boolean
}

/** Status of a domain registration, exactly as the API sends it. */
export enum DomainStatus {
  Active = 'Active',
  PendingRegistration = 'PendingRegistration',
  PendingTransfer = 'PendingTransfer',
  Expired = 'Expired',
  Redemption = 'Redemption',
  Transferred = 'Transferred',
  Cancelled = 'Cancelled',
}

/** Closed set of DNS record types a domain's zone can hold. */
export const DnsRecordType = {
  A: 'A',
  Aaaa: 'AAAA',
  Cname: 'CNAME',
  Mx: 'MX',
  Txt: 'TXT',
  Ns: 'NS',
  Srv: 'SRV',
} as const

/** One of the DNS record types. */
export type DnsRecordType = (typeof DnsRecordType)[keyof typeof DnsRecordType]

/** Kind of expiry reminder recorded against a domain, exactly as the API sends it. */
export enum DomainReminderType {
  ThirtyDaysBeforeExpiry = 'ThirtyDaysBeforeExpiry',
  FourteenDaysBeforeExpiry = 'FourteenDaysBeforeExpiry',
  SevenDaysBeforeExpiry = 'SevenDaysBeforeExpiry',
  OneDayBeforeExpiry = 'OneDayBeforeExpiry',
  ExpiredNotice = 'ExpiredNotice',
  RedemptionNotice = 'RedemptionNotice',
  Unknown = 'Unknown',
}

/** Locale key of the label shown for each domain reminder type. */
export const DOMAIN_REMINDER_TYPE_LABEL_KEYS: Record<DomainReminderType, string> = {
  [DomainReminderType.ThirtyDaysBeforeExpiry]: 'clients.domainRemindersTable.types.thirtyDaysBeforeExpiry',
  [DomainReminderType.FourteenDaysBeforeExpiry]: 'clients.domainRemindersTable.types.fourteenDaysBeforeExpiry',
  [DomainReminderType.SevenDaysBeforeExpiry]: 'clients.domainRemindersTable.types.sevenDaysBeforeExpiry',
  [DomainReminderType.OneDayBeforeExpiry]: 'clients.domainRemindersTable.types.oneDayBeforeExpiry',
  [DomainReminderType.ExpiredNotice]: 'clients.domainRemindersTable.types.expiredNotice',
  [DomainReminderType.RedemptionNotice]: 'clients.domainRemindersTable.types.redemptionNotice',
  [DomainReminderType.Unknown]: 'clients.domainRemindersTable.types.unknown',
}
