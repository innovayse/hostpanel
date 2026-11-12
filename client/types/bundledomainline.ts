/**
 * The domain a visitor picked for a bundle that includes one, priced for the bundle's years.
 *
 * @module types/bundledomainline
 */

import type { DomainOrderAction } from '~/types/domainorderaction'

/** The domain line of a bundle order. */
export interface BundleDomainLine {
  /** The domain product the line hangs on. */
  productId: number
  /** Fully qualified domain name. */
  name: string
  /** Whether the domain is registered or transferred in. */
  action: DomainOrderAction
  /** The transfer's authorization code; empty for a registration or when not given yet. */
  eppCode: string
  /** TLD extension without its dot, e.g. "com". */
  tld: string
  /** The line's estimated price: the one-year price times the bundle's years. */
  amount: number
  /** Localised label of the registration period, e.g. "2 Years". */
  cycleLabel: string
}
