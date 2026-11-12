/**
 * Tests for `pages/client/domains/transfer.vue` against the real domains store, the real billing and
 * domains API composables and the real transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked): the prefilled form, the order and the outcomes.
 *
 * @module tests/pages/client/domains/transfer.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { stubClientRuntime } from '~/tests/helpers/mountClient'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import TransferPage from '~/pages/client/domains/transfer.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page uses. */
const PATHS = {
  gatewayMethods: '/api/portal/order/payment-methods',
  transferOrder: '/api/portal/client/domains/transfer-order',
} as const

/** The `data-testid` of an element the page always renders once its payment methods are read. */
const PAGE_MARKER = 'transfer-back'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** A gateway. */
const card = { module: 'card_gateway', displayName: 'Card gateway', supportsStoredCredentials: false }

beforeEach(async () => {
  stubClientRuntime({ query: { domain: 'moving.org', eppcode: 'AUTH-1' } })
  table = { [`GET ${PATHS.gatewayMethods}`]: { body: [card] } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('domain transfer page', () => {
  it('prefills the domain and the code from the query', async () => {
    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    expect((wrapper.find('[data-testid="transfer-domain"]').element as HTMLInputElement).value).toBe('moving.org')
    expect((wrapper.find('[data-testid="transfer-epp"]').element as HTMLInputElement).value).toBe('AUTH-1')
  })

  it('has a placeholder on every field', async () => {
    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="transfer-domain"]').attributes('placeholder')).toBe('client.domainTransfer.domainPlaceholder')
    expect(wrapper.find('[data-testid="transfer-epp"]').attributes('placeholder')).toBe('client.domainTransfer.eppPlaceholder')
  })

  it('preselects the first saved payment method and enables the order', async () => {
    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="transfer-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('says so, and cannot order, when there is no payment method', async () => {
    table[`GET ${PATHS.gatewayMethods}`] = { body: [] }

    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="transfer-no-payment"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="transfer-submit"]').attributes('disabled')).toBeDefined()
  })

  it('places the order and links to the invoice', async () => {
    table[`POST ${PATHS.transferOrder}`] = { body: { orderId: 3, invoiceId: 90 } }
    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="transfer-success"]').exists()).toBe(true))
    expect(bodyOf(requestsTo(stub.requests, PATHS.transferOrder)[0])).toMatchObject({ domain: 'moving.org', eppcode: 'AUTH-1', years: 1, paymentmethod: 'card_gateway' })
    expect(wrapper.find('[data-testid="transfer-pay-invoice"]').attributes('href')).toBe('/client/invoices/90')
  })

  it('shows the refusal and keeps the form when the order fails', async () => {
    table[`POST ${PATHS.transferOrder}`] = { status: 409, body: { detail: 'Domain is locked.' } }
    const wrapper = await mountRenderedClientPage(TransferPage, PAGE_MARKER)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Domain is locked.'))
    expect(wrapper.find('[data-testid="transfer-success"]').exists()).toBe(false)
  })
})
