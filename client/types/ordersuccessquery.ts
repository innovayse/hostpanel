/**
 * What the order-success link carries in its query string.
 *
 * @module types/ordersuccessquery
 */

import type { DomainOrderAction } from '~/types/domainorderaction'

/** What the order-success link carries in its query string. */
export interface OrderSuccessQuery {
  /** The order number the backend assigned, shown as-is. */
  order: string
  /** The order's invoice id; the page reads the charged amount from that invoice. */
  invoice?: number
  /** The domain bought with the order, when there was one. */
  domain?: string
  /** What was done to {@link domain}: registered or transferred. */
  action?: DomainOrderAction
  /** True when everything in the order was digital, so the page offers no hosting setup. */
  digital?: boolean
}
