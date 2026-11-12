/**
 * One billing cycle a bundle component can be bought on.
 *
 * @module types/bundlecycleoption
 */

import type { PricingCycle } from '~/types/pricingcycle'

/** One selectable billing cycle of a bundle component, with its price. */
export interface BundleCycleOption {
  /** The cycle. */
  cycle: PricingCycle
  /** Its name in the reader's language, e.g. "Annually". */
  label: string
  /** The component's price for the cycle, as the catalogue quotes it in the payer's currency. */
  amount: number
  /** The same price, formatted. */
  price: string
}
