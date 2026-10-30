/**
 * A single line in the shopping cart: a hosting plan or a domain.
 *
 * @module types/cartitem
 */

import type { CartItemType } from '~/types/cartitemtype'
import type { DomainOrderAction } from '~/types/domainorderaction'
import type { PricingCycle } from '~/types/pricingcycle'
import type { ProductType } from '~/types/producttype'

/** A single line in the shopping cart: a hosting plan or a domain. */
export interface CartItem {
  /** Product ID (generic domain product PID for domain items) */
  pid: number
  /** Human-readable plan name or domain name */
  name: string
  /** Billing cycle the line is ordered on; a free plan is ordered on the monthly cycle at zero. */
  billingcycle: PricingCycle
  /** Localised cycle label, e.g. "Monthly", "1 Year" */
  cycleLabel: string
  /**
   * Numeric price for one billing cycle, in {@link currency}. Stored as sent by the API,
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
  /** Item type: hosting or domain. Defaults to "hosting" if omitted. */
  itemType?: CartItemType
  /** Domain action: register or transfer */
  domainAction?: DomainOrderAction
  /** EPP/auth code for domain transfers */
  eppCode?: string
  /** TLD extension, e.g. "com", "net" */
  tld?: string
  /** Registration/transfer period in years */
  years?: number
  /**
   * Type of the catalogue product the line orders, when the page that added it knew it. A line
   * saved before this field existed has none, and is treated like any non-digital line.
   */
  productType?: ProductType
}
