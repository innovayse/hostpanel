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
  /** Formatted price string, e.g. "$9.99" (used for hosting items) */
  price: string
  /** Currency prefix, e.g. "$" (used for hosting items) */
  prefix: string
  /** Raw numeric price string, e.g. "9.99" (used for hosting items) */
  rawPrice: string
  /** Base price in AMD for domain items — converted to display currency at render time */
  priceAmd?: number
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
