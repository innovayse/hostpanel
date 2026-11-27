/**
 * Unit tests for CheckoutDomainFields: every domain registration and transfer in the cart is asked the extra fields the
 * registrar plugin of its extension asks for, the values ride on the cart line in memory, a hosting line is never asked, and the
 * API's refusal of a field shows next to it. The stores, the API composables and the transport run for real against a stub BFF.
 *
 * @module tests/components/checkout/CheckoutDomainFields.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import CheckoutDomainFields from '~/components/checkout/CheckoutDomainFields.vue'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { cartLine } from '~/tests/helpers/checkoutFlow'
import { readOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { PricingCycle } from '~/types/pricingcycle'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

/** The one required field the registrar plugin declares for the invented zone. */
const FIELD = {
  key: 'holder-number',
  label: 'Holder number',
  type: 'Text',
  required: true,
  description: null,
  placeholder: null,
  pattern: null,
  minLength: null,
  maxLength: null,
  options: [],
}

/**
 * Builds a domain line of the cart.
 *
 * @param action - Whether the line registers or transfers the domain.
 * @returns The line.
 */
const domainLine = (action: DomainOrderAction): CartItem => ({
  pid: 9,
  name: 'name.alpha',
  billingcycle: PricingCycle.Annual,
  cycleLabel: '1 Year',
  amount: 10,
  currency: 'USD',
  domain: 'name.alpha',
  itemType: CartItemType.Domain,
  domainAction: action,
  years: 1,
})

beforeEach(async () => {
  table = { [readOf(CatalogPath.DomainFields)]: { body: [FIELD] } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('CheckoutDomainFields', () => {
  it('asks the fields of a registration and keeps the typed values on the cart line', async () => {
    const wrapper = mountClient(CheckoutDomainFields)
    const cart = useCartStore()
    cart.items = [domainLine(DomainOrderAction.Register)]

    /** The input of the one field. */
    const input = () => wrapper.find('[data-testid="checkout-domain-fields-name.alpha-holder-number"]')
    await waitUntil(() => expect(input().exists()).toBe(true))
    expect(requestsTo(stub.requests, CatalogPath.DomainFields)[0]?.url)
      .toBe(CatalogPath.DomainFields + '?domain=name.alpha&operation=register&holder=individual')
    await input().setValue('N-1')

    expect(cart.items[0]?.extendedAttributes).toEqual({ 'holder-number': 'N-1' })
  })

  it('asks a transfer for the fields of a transfer', async () => {
    const wrapper = mountClient(CheckoutDomainFields)
    useCartStore().items = [domainLine(DomainOrderAction.Transfer)]

    await waitUntil(() => expect(requestsTo(stub.requests, CatalogPath.DomainFields)).toHaveLength(1))

    expect(requestsTo(stub.requests, CatalogPath.DomainFields)[0]?.url).toContain('operation=transfer')
    expect(wrapper.find('[data-testid="checkout-domain-fields"]').exists()).toBe(true)
  })

  it('draws nothing and asks nothing for a cart of hosting lines', async () => {
    const wrapper = mountClient(CheckoutDomainFields)
    useCartStore().items = [cartLine(5, 12)]

    await waitUntil(() => expect(wrapper.find('[data-testid="checkout-domain-fields"]').exists()).toBe(false))

    expect(requestsTo(stub.requests, CatalogPath.DomainFields)).toHaveLength(0)
  })

  it('draws no form when the registrar asks for nothing', async () => {
    table[readOf(CatalogPath.DomainFields)] = { body: [] }
    const wrapper = mountClient(CheckoutDomainFields)
    useCartStore().items = [domainLine(DomainOrderAction.Register)]

    await waitUntil(() => expect(requestsTo(stub.requests, CatalogPath.DomainFields)).toHaveLength(1))

    expect(wrapper.find('[data-testid="checkout-domain-fields-name.alpha"]').exists()).toBe(false)
  })

  it('shows the refusal of a field next to it', async () => {
    const wrapper = mountClient(CheckoutDomainFields)
    useCartStore().items = [domainLine(DomainOrderAction.Register)]
    useCheckoutStore().fieldErrors = { 'holder-number': 'A required registry detail is missing.' }

    await waitUntil(() => expect(wrapper.text()).toContain('A required registry detail is missing.'))
  })
})
