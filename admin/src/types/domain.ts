/**
 * A registered domain and everything that hangs off it — nameservers, DNS records,
 * forwarding rules and renewal reminders.
 */

import type { Product } from './product'

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
