/**
 * The domain a visitor entered for a bundle, before it is priced.
 *
 * @module types/bundledomainchoice
 */

import type { DomainOrderAction } from '~/types/domainorderaction'

/** What the bundle's domain step reports. */
export interface BundleDomainChoice {
  /** Whether the domain is registered or transferred in. */
  action: DomainOrderAction
  /** Fully qualified domain name. */
  name: string
  /** The transfer's authorization code; empty for a registration. */
  eppCode: string
}
