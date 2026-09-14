/** A single line in the shopping cart — a hosting plan or a domain. */
export interface CartItem {
  /** Product ID (generic domain product PID for domain items) */
  pid: number
  /** Human-readable plan name or domain name */
  name: string
  /** Billing cycle key, e.g. "monthly", "annually" */
  billingcycle: string
  /** Localised cycle label, e.g. "Monthly", "1 Year" */
  cycleLabel: string
  /**
   * Numeric price for one billing cycle, in {@link currency}. Stored as sent by the API —
   * never converted, never re-labelled. An item whose `currency` no longer matches the payer's
   * current currency (they changed it after adding) is excluded from the cart total and
   * flagged for removal rather than silently reprised; see `stores/cart.ts`.
   */
  amount: number
  /** ISO 4217 code {@link amount} is priced in, e.g. "AMD". */
  currency: string
  /** Domain name for hosting or domain items */
  domain?: string
  /** Hostname for VPS/server items */
  hostname?: string
  /** Account username for hosting items */
  username?: string
  /** Account password for hosting items */
  password?: string
  /** Item type: "hosting" or "domain". Defaults to "hosting" if omitted. */
  itemType?: 'hosting' | 'domain'
  /** Domain action: "register" or "transfer" */
  domainAction?: 'register' | 'transfer'
  /** EPP/auth code for domain transfers */
  eppCode?: string
  /** TLD extension, e.g. "com", "net" */
  tld?: string
  /** Registration/transfer period in years */
  years?: number
}
