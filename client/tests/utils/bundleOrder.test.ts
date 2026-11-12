/**
 * Unit tests for `utils/bundleOrder.ts`: the cart lines a bundle becomes and when it is complete.
 *
 * @module tests/utils/bundleOrder.test
 */

import { describe, expect, it } from 'vitest'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import type { BundleDomainLine } from '~/types/bundledomainline'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundle } from '~/types/catalogbundle'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { PricingCycle } from '~/types/pricingcycle'
import { ProductType } from '~/types/producttype'
import { bundleCartItems, isBundleComplete, toCartBundle } from '~/utils/bundleOrder'

/** A bundle of a hosting plan and a mailbox plan with a domain for two years. */
const bundle: CatalogBundle = {
  id: 7,
  name: 'Starter pack',
  description: null,
  includesDomain: true,
  domainYears: 2,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  components: [
    { productId: 1, name: 'Hosting', isPrimary: true },
    { productId: 2, name: 'Mail', isPrimary: false },
  ],
}

/**
 * Builds a component row with a chosen annual cycle.
 *
 * @param productId The product id.
 * @param isPrimary Whether it is the primary component.
 * @param chosen Whether a cycle is chosen.
 * @returns The row.
 */
const row = (productId: number, isPrimary: boolean, chosen = true): BundleComponentRow => {
  const option = { cycle: PricingCycle.Annual, label: 'Annually', amount: 50, price: '$50.00' }
  return { productId, name: `Product ${productId}`, isPrimary, productType: ProductType.SharedHosting, options: [option], selected: chosen ? option : null }
}

/** A transfer of example.com priced for the bundle's two years. */
const domain: BundleDomainLine = {
  productId: 9, name: 'example.com', action: DomainOrderAction.Transfer, eppCode: 'abc', tld: 'com', amount: 20, cycleLabel: '2 Years',
}

describe('bundleCartItems', () => {
  it('makes one line per component, tagged with the bundle, and the primary one carries the domain', () => {
    const items = bundleCartItems(bundle, [row(1, true), row(2, false)], domain, 'USD')

    expect(items.slice(0, 2).map(i => [i.pid, i.bundleId, i.domain])).toEqual([[1, 7, 'example.com'], [2, 7, undefined]])
    expect(items[0]).toMatchObject({ billingcycle: PricingCycle.Annual, amount: 50, currency: 'USD', productType: ProductType.SharedHosting })
  })

  it('adds the domain line with the action, the code and the years the bundle fixes', () => {
    const items = bundleCartItems(bundle, [row(1, true)], domain, 'USD')

    expect(items[1]).toMatchObject({
      pid: 9, itemType: CartItemType.Domain, domain: 'example.com', domainAction: DomainOrderAction.Transfer,
      eppCode: 'abc', years: 2, amount: 20, tld: 'com', bundleId: 7,
    })
  })

  it('leaves the code off a registration and adds no domain line to a bundle without one', () => {
    const registered = bundleCartItems(bundle, [row(1, true)], { ...domain, action: DomainOrderAction.Register }, 'USD')
    const plain = bundleCartItems({ ...bundle, includesDomain: false, domainYears: null }, [row(1, true)], null, 'USD')

    expect(registered[1]?.eppCode).toBeUndefined()
    expect(plain).toHaveLength(1)
  })
})

describe('isBundleComplete', () => {
  it('needs a cycle for every component and a domain when the bundle includes one', () => {
    expect(isBundleComplete(bundle, [row(1, true), row(2, false)], domain)).toBe(true)
    expect(isBundleComplete(bundle, [row(1, true), row(2, false, false)], domain)).toBe(false)
    expect(isBundleComplete(bundle, [row(1, true)], null)).toBe(false)
    expect(isBundleComplete({ ...bundle, includesDomain: false }, [row(1, true)], null)).toBe(true)
  })
})

describe('toCartBundle', () => {
  it('keeps only what the cart needs', () => {
    expect(toCartBundle(bundle)).toEqual({ id: 7, name: 'Starter pack', discountType: BundleDiscountType.Percentage, discountAmount: 10, discountCurrency: null })
  })
})
