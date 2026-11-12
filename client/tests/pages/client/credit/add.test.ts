/**
 * Tests for `pages/client/credit/add.vue` against the real credit store, the real billing API
 * composable and the real transport, with a stub BFF answering on a real socket (nothing of ours is
 * mocked): the form is held to the limits the credit reports, the top-up invoice is raised for the
 * amount, and the client is sent to pay it; a refusal stays on the page with the backend's sentence.
 *
 * @module tests/pages/client/credit/add.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { stubClientRuntime } from '~/tests/helpers/mountClient'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { settle } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import AddFundsPage from '~/pages/client/credit/add.vue'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page uses. */
const PATHS = {
  credit: '/api/portal/client/credit',
  topUps: '/api/portal/client/credit/top-ups',
} as const

/** The `data-testid` of the page's root, on screen once its read has ended. */
const PAGE_ROOT = 'client-credit-add'

/** Stands in for Nuxt's `navigateTo`, a framework function, not ours. */
const navigate = vi.fn()

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** The credit every case reads: limits 5 to 100. */
const credit = {
  balance: 0,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 100,
  entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
}

/**
 * The top-up requests the stub received.
 *
 * @returns The requests, oldest first.
 */
const topUps = () => requestsTo(stub.requests, PATHS.topUps)

beforeEach(async () => {
  navigate.mockReset()
  stubClientRuntime()
  vi.stubGlobal('navigateTo', navigate)
  table = { [`GET ${PATHS.credit}`]: { body: credit } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('add funds page', () => {
  it('shows the form with the limits the backend reported', async () => {
    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="credit-add-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-add-limits"]').exists()).toBe(true)
  })

  it('raises the top-up invoice for the amount and sends the client to pay it', async () => {
    table[`POST ${PATHS.topUps}`] = { body: 88 }
    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('40')
    await wrapper.get('form').trigger('submit')
    await waitUntil(() => expect(navigate).toHaveBeenCalledWith('/client/invoices/88/pay'))

    expect(topUps()).toHaveLength(1)
    expect(bodyOf(topUps()[0])).toEqual({ amount: 40 })
  })

  it('does not ask the backend for an amount outside the limits', async () => {
    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('1000')
    await wrapper.get('form').trigger('submit')
    await settle()

    expect(topUps()).toHaveLength(0)
    expect(navigate).not.toHaveBeenCalled()
  })

  it('stays on the page and shows the sentence when the backend refuses the amount', async () => {
    table[`POST ${PATHS.topUps}`] = { status: 422, body: { detail: 'The amount is outside the limits.' } }
    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('40')
    await wrapper.get('form').trigger('submit')
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true))

    expect(wrapper.get('[data-testid="ui-form-error"]').text()).toContain('The amount is outside the limits.')
    expect(navigate).not.toHaveBeenCalled()
  })

  it('shows the failure instead of a form that cannot know its limits', async () => {
    table[`GET ${PATHS.credit}`] = { status: 404, body: { type: PortalErrorCode.ClientProfileNotFound, detail: 'No client profile.' } }

    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="credit-add-load-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-add-form"]').exists()).toBe(false)
  })

  it('links back to the credit page', async () => {
    const wrapper = await mountRenderedClientPage(AddFundsPage, PAGE_ROOT)

    expect(wrapper.get('[data-testid="credit-add-back"]').attributes('href')).toBe('/client/credit')
  })
})
