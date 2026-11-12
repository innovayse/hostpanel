/**
 * Tests for `pages/client/credit/index.vue` against the real credit store, the real billing API
 * composable and the real transport, with a stub BFF answering on a real socket (nothing of ours is
 * mocked): the balance widget, the history table, the paging of the history and the failure state.
 *
 * @module tests/pages/client/credit/index.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { stubClientRuntime } from '~/tests/helpers/mountClient'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import CreditPage from '~/pages/client/credit/index.vue'
import { CreditMovement } from '~/types/creditmovement'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path the page reads. */
const CREDIT_PATH = '/api/portal/client/credit'

/** The `data-testid` of the page's root, on screen once its read has ended. */
const PAGE_ROOT = 'client-credit'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a credit with the given lines.
 *
 * @param lines - Number of history lines on the page.
 * @param totalPages - Pages of history the backend reports.
 * @returns The credit.
 */
const credit = (lines: number, totalPages = 1) => ({
  balance: 75,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: {
    items: Array.from({ length: lines }, (_, i) => ({
      id: 100 + i,
      date: '2026-10-01T09:00:00Z',
      movement: CreditMovement.AdminAdd,
      change: 25,
      balanceAfter: 25 * (i + 1),
      invoiceId: null
    })),
    pageNumber: 1,
    pageSize: 10,
    totalCount: lines,
    totalPages
  }
})

/**
 * The credit reads the stub received.
 *
 * @returns The requests, oldest first.
 */
const reads = () => requestsTo(stub.requests, CREDIT_PATH)

beforeEach(async () => {
  stubClientRuntime()
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('credit page', () => {
  it('reads the first page of the credit and shows the balance and the history', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(2) }

    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)

    expect(reads()).toHaveLength(1)
    expect(queryOf(reads()[0]!)).toEqual({ page: '1', pageSize: '10' })
    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('75.00')
    expect(wrapper.find('[data-testid="credit-entry-100"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-entry-101"]').exists()).toBe(true)
  })

  it('offers the way to add funds', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(0) }

    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)

    expect(wrapper.get('[data-testid="credit-add-funds"]').attributes('href')).toBe('/client/credit/add')
    expect(wrapper.find('[data-testid="credit-history-empty"]').exists()).toBe(true)
  })

  it('shows no pagination when the whole history fits one page', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(3, 1) }

    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="ui-pagination-next"]').exists()).toBe(false)
  })

  it('reads the next page when the client asks for it', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(10, 3) }
    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)

    await wrapper.get('[data-testid="ui-pagination-next"]').trigger('click')
    await waitUntil(() => expect(reads()).toHaveLength(2))

    expect(queryOf(reads()[1]!)).toEqual({ page: '2', pageSize: '10' })
  })

  it('offers a retry on the failed read that asks for the same page again', async () => {
    /** Whether the stub refuses the read of the second page. */
    let refusePageTwo = false
    table[`GET ${CREDIT_PATH}`] = (request) => refusePageTwo && queryOf(request).page === '2'
      ? { status: 404, body: { type: PortalErrorCode.ClientProfileNotFound, detail: 'No client profile.' } }
      : { body: credit(10, 3) }
    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)
    refusePageTwo = true
    await wrapper.get('[data-testid="ui-pagination-next"]').trigger('click')
    await waitUntil(() => expect(wrapper.find('[data-testid="credit-error"]').exists()).toBe(true))

    refusePageTwo = false
    await wrapper.get('[data-testid="credit-error-retry"]').trigger('click')
    // The sentence clears the moment the retry starts; the balance is what appears once its answer has been handled.
    await waitUntil(() => expect(wrapper.find('[data-testid="credit-balance"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="credit-error"]').exists()).toBe(false)
    expect(queryOf(reads()[reads().length - 1]!)).toEqual({ page: '2', pageSize: '10' })
    expect(wrapper.find('[data-testid="credit-balance"]').exists()).toBe(true)
  })

  it('shows the sentence of a failed read instead of the balance', async () => {
    table[`GET ${CREDIT_PATH}`] = { status: 404, body: { type: PortalErrorCode.ClientProfileNotFound, detail: 'No client profile.' } }

    const wrapper = await mountRenderedClientPage(CreditPage, PAGE_ROOT)

    expect(wrapper.find('[data-testid="credit-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-balance"]').exists()).toBe(false)
  })
})
