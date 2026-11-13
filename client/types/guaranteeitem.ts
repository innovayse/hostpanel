/**
 * Guarantee item type: one promise of the guarantees section.
 *
 * @module types/guaranteeitem
 */

import type { GuaranteeKey } from '~/constants/guaranteeKey'

/** A guarantee the operator offers. */
export interface GuaranteeItem {
  /** Which promise it is. */
  key: GuaranteeKey
  /** Length of the promise in days; only the money-back promise has one. */
  days?: number
}
