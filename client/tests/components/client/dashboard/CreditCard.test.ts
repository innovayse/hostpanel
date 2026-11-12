/**
 * Unit tests for ClientDashboardCreditCard: the dashboard widget reads the credit when it appears, shows the balance with the way to
 * add funds, and shows nothing at all when the credit cannot be read.
 *
 * The real credit store, billing API composable and transport run against a stub server playing the BFF; the assertions are on the
 * request that reached it.
 *
 * @module tests/components/client/dashboard/CreditCard.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import CreditCard from '~/components/client/dashboard/CreditCard.vue'
import { CREDIT_PAGE_SIZE, useCreditStore } from '~/stores/credit'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that answers the credit balance and its history. */
const CREDIT_PATH = '/api/portal/client/credit'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** How many times the credit is asked for when the first answer is a server failure: the transport asks once more. */
const FAILED_READ_REQUESTS = 2

/**
 * Mounts the widget and waits until the credit it reads on appearing has been handled.
 *
 * @param requests - How many credit requests the read makes before it ends: one, or {@link FAILED_READ_REQUESTS} for a failing server.
 * @returns The wrapper.
 */
const mountCard = async (requests = 1) => {
  const wrapper = mountClient(CreditCard)
  await waitUntil(() => expect(requestsTo(stub.requests, CREDIT_PATH)).toHaveLength(requests))
  await waitUntil(() => expect(useCreditStore().loading).toBe(false))
  await wrapper.vm.$nextTick()
  return wrapper
}

describe('ClientDashboardCreditCard', () => {
  beforeEach(async () => {
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('reads the first page of the credit and shows the balance', async () => {
    table[`GET ${CREDIT_PATH}`] = {
      body: {
        balance: 25,
        currency: 'USD',
        topUpMinimum: 5,
        topUpMaximum: 1000,
        entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
      }
    }

    const wrapper = await mountCard()

    expect(requestsTo(stub.requests, CREDIT_PATH).map((request) => queryOf(request))).toEqual([{ page: '1', pageSize: String(CREDIT_PAGE_SIZE) }])
    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('25.00')
    expect(wrapper.find('[data-testid="credit-add-funds"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-view"]').exists()).toBe(true)
  })

  it('shows nothing when the credit cannot be read', async () => {
    table[`GET ${CREDIT_PATH}`] = { status: 500, body: { detail: 'down' } }

    const wrapper = await mountCard(FAILED_READ_REQUESTS)

    expect(wrapper.find('[data-testid="credit-balance-card"]').exists()).toBe(false)
  })

  it('asks for the credit once more after a server failure, and no more', async () => {
    table[`GET ${CREDIT_PATH}`] = { status: 500, body: { detail: 'down' } }

    await mountCard(FAILED_READ_REQUESTS)

    expect(requestsTo(stub.requests, CREDIT_PATH)).toHaveLength(FAILED_READ_REQUESTS)
  })
})
