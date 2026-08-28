/** One domain on the client's account, as `GET /api/portal/client/domains` returns it (backend `DomainDto`). */
export interface ClientDomain {
  /** Domain primary key. */
  id: number
  /** FK to the owning client. */
  clientId: number
  /** Full domain name (e.g. "example.com"). */
  name: string
  /** Top-level domain including the dot (e.g. ".com"). */
  tld: string
  /** Current lifecycle status (e.g. "Active", "Expired"). */
  status: string
  /** Domain registration date (ISO 8601 UTC). */
  registeredAt: string
  /** Domain expiration date (ISO 8601 UTC). */
  expiresAt: string
  /** Whether the domain is set to auto-renew at expiration. */
  autoRenew: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Whether the domain is locked against unauthorized transfers. */
  isLocked: boolean
  /** Reference ID from the registrar's system. */
  registrarRef: string | null
  /** Authorization code for transfer. */
  eppCode: string | null
  /** FK to linked service (e.g. hosting plan). */
  linkedServiceId: number | null
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring registration price. */
  recurringAmount: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Applied promotion/coupon code. */
  promotionCode: string | null
  /** External payment subscription reference. */
  subscriptionId: string | null
  /** Free-text admin notes. */
  adminNotes: string | null
  /** FK to the order that created this domain. */
  orderId: number | null
  /** Order type: "Register" or "Transfer". */
  orderType: string
  /** Whether DNS management is enabled. */
  dnsManagement: boolean
  /** Whether email forwarding is enabled. */
  emailForwarding: boolean
  /** ISO 4217 currency code for the price. */
  priceCurrency: string
  /** Next renewal payment due date (ISO 8601 UTC). */
  nextDueDate: string
  /** Name of the registrar module. */
  registrar: string | null
  /** Registration period in years. */
  registrationPeriod: number
}
