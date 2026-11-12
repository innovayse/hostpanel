/**
 * Tests for `pages/client/order-success.vue` against the real client and invoice payment stores,
 * the real API composables and the real transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked): the order summary read from the link and the refresh before opening
 * the services page.
 *
 * @module tests/pages/client/order-success.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import OrderSuccessPage from '~/pages/client/order-success.vue'
import { Permission } from '~/types/permission'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads. */
const PATHS = {
  me: '/api/portal/client/me',
  services: '/api/portal/client/services',
  invoice: '/api/portal/client/invoices/7',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

beforeEach(async () => {
  table = {
    [`GET ${PATHS.me}`]: { body: makeClientUser({ permissions: Permission.All }) },
    [`GET ${PATHS.services}`]: { body: [] },
    [`GET ${PATHS.invoice}`]: { body: { id: 7, total: 25, currency: 'USD' } },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('client order-success page', () => {
  it('shows the order number from the link and the amount from the order invoice', async () => {
    stubAuthRuntime({ query: { order: 'A-1001', invoice: '7' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="order-success-amount"]').text()).toContain('$25.00'))

    expect(requestsTo(stub.requests, PATHS.invoice).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('[data-testid="order-success-number"]').text()).toContain('A-1001')
  })

  it('shows dashes and reads no invoice when the link carries neither an order nor an invoice', async () => {
    stubAuthRuntime({ query: { amount: '$9.00' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(stub.requests).toHaveLength(0)
    expect(wrapper.find('[data-testid="order-success-number"]').text()).toContain('—')
    expect(wrapper.find('[data-testid="order-success-amount"]').text()).toContain('—')
  })

  it('hides the domain rows and the manage-domain button for a hosting-only order', async () => {
    stubAuthRuntime({ query: { order: '1' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-domain"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-success-action"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-success-manage-domain"]').exists()).toBe(false)
  })

  it('shows the domain, its action and the manage-domain button when a domain was bought', async () => {
    stubAuthRuntime({ query: { order: '1', domain: 'example.com', action: 'transfer' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-domain"]').text()).toContain('example.com')
    expect(wrapper.find('[data-testid="order-success-action"]').text()).toContain('checkout.success.transfer')
    expect(wrapper.find('[data-testid="order-success-manage-domain"]').attributes('href')).toBe('/client/domains')
  })

  it('names a registration as such', async () => {
    stubAuthRuntime({ query: { domain: 'example.com', action: 'register' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-action"]').text()).toContain('checkout.success.registration')
  })

  it('links to the invoices', async () => {
    stubAuthRuntime()

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-invoices"]').attributes('href')).toBe('/client/invoices')
  })

  it('offers the hosting setup after an order that held a hosting service', async () => {
    stubAuthRuntime({ query: { order: '1' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-setup-service"]').exists()).toBe(true)
  })

  it('offers no hosting setup after an order of digital goods only, but still links to the invoices', async () => {
    stubAuthRuntime({ query: { order: '1', digital: '1' } })

    const wrapper = await mountAuthPage(OrderSuccessPage)

    expect(wrapper.find('[data-testid="order-success-setup-service"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-success-invoices"]').exists()).toBe(true)
  })

  it('reloads the services before opening the services page', async () => {
    stubAuthRuntime()
    const wrapper = await mountAuthPage(OrderSuccessPage)

    await wrapper.find('[data-testid="order-success-setup-service"]').trigger('click')
    await waitUntil(() => expect(navigateTo).toHaveBeenCalledWith('/client/services'))

    expect(requestsTo(stub.requests, PATHS.services).map(request => request.method)).toEqual(['GET'])
  })
})
