/**
 * The cart lines of a bundle: one per component product, plus the domain line when the bundle
 * includes a domain.
 *
 * The lines are what the order sends (`items`, with the bundle's id beside them); the prices on
 * them are the storefront's estimate, and the backend prices and validates the order itself.
 *
 * @module utils/bundleOrder
 */

import { DOMAIN_BILLING_CYCLE } from '~/constants/pricingCycle'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import type { BundleDomainLine } from '~/types/bundledomainline'
import type { CartBundle } from '~/types/cartbundle'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import type { CatalogBundle } from '~/types/catalogbundle'

/**
 * The part of a catalogue bundle the cart keeps.
 *
 * @param bundle The catalogue bundle.
 * @returns The bundle as the cart stores it.
 */
export const toCartBundle = (bundle: CatalogBundle): CartBundle => ({
  id: bundle.id,
  name: bundle.name,
  discountType: bundle.discountType,
  discountAmount: bundle.discountAmount,
  discountCurrency: bundle.discountCurrency,
})

/**
 * Whether every part of the bundle is chosen: each component has a priced cycle and a bundle that
 * includes a domain has one picked.
 *
 * @param bundle The catalogue bundle.
 * @param rows The bundle's components with the visitor's cycle choices.
 * @param domain The picked domain; null when none is picked yet.
 * @returns True when the bundle can be put in the cart.
 */
export const isBundleComplete = (
  bundle: CatalogBundle,
  rows: readonly BundleComponentRow[],
  domain: BundleDomainLine | null,
): boolean =>
  rows.length > 0 && rows.every(row => row.selected !== null) && (!bundle.includesDomain || domain !== null)

/**
 * Builds the cart lines of a bundle.
 *
 * The primary component also carries the picked domain name, as the service's domain. A transfer
 * line keeps its authorization code only when one was typed.
 *
 * @param bundle The catalogue bundle.
 * @param rows The bundle's components with the visitor's cycle choices; every row must have one.
 * @param domain The picked domain; null for a bundle without one.
 * @param currency The ISO 4217 code every amount is priced in.
 * @returns The lines, components first and the domain last.
 */
export const bundleCartItems = (
  bundle: CatalogBundle,
  rows: readonly BundleComponentRow[],
  domain: BundleDomainLine | null,
  currency: string,
): CartItem[] => {
  const items: CartItem[] = []
  for (const row of rows) {
    if (row.selected === null) {
      continue
    }
    items.push({
      pid: row.productId,
      name: row.name,
      billingcycle: row.selected.cycle,
      cycleLabel: row.selected.label,
      amount: row.selected.amount,
      currency,
      productType: row.productType,
      bundleId: bundle.id,
      domain: row.isPrimary && domain ? domain.name : undefined,
    })
  }
  if (domain) {
    items.push({
      pid: domain.productId,
      name: domain.name,
      billingcycle: DOMAIN_BILLING_CYCLE,
      cycleLabel: domain.cycleLabel,
      amount: domain.amount,
      currency,
      domain: domain.name,
      itemType: CartItemType.Domain,
      domainAction: domain.action,
      eppCode: domain.action === DomainOrderAction.Transfer && domain.eppCode ? domain.eppCode : undefined,
      tld: domain.tld,
      years: bundle.domainYears ?? undefined,
      bundleId: bundle.id,
    })
  }
  return items
}
