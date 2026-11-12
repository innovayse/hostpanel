/**
 * Tests for FraudLogView: the log comes from the API with decisions and reasons as translated
 * labels, the client and order link to their screens, the two filters reload the first page, and a
 * refused load can be retried.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import FraudLogView from '@/views/FraudLogView.vue'
import { FraudCheckKind, FraudDecision, FraudReasonCode } from '@/types/fraud'
import type { FraudLogEntry } from '@/types/fraud'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { pickOption } from '../helpers/select'

/** Request line of the unfiltered first page. */
const FIRST_PAGE = 'GET /admin/fraud/log?page=1&pageSize=20'

/** An order that was held for review for two reasons. */
const held: FraudLogEntry = {
  id: 3,
  kind: FraudCheckKind.Order,
  decision: FraudDecision.Review,
  clientId: 12,
  orderId: 40,
  ipAddress: '203.0.113.7',
  email: 'buyer@example.test',
  score: 62,
  reasons: [FraudReasonCode.OrderVelocity, FraudReasonCode.AnonymousIp],
  createdAt: '2026-11-01T10:00:00Z',
}

/** A registration that passed with no reasons and nothing to link to. */
const clean: FraudLogEntry = {
  id: 4,
  kind: FraudCheckKind.Registration,
  decision: FraudDecision.Approved,
  clientId: null,
  orderId: null,
  ipAddress: null,
  email: null,
  score: 0,
  reasons: [],
  createdAt: '2026-11-02T10:00:00Z',
}

/**
 * Wraps entries in the page shape a list endpoint answers with.
 *
 * @param items - The entries of the page.
 * @param totalCount - Total across all pages.
 * @returns The page.
 */
const pageOf = (items: FraudLogEntry[], totalCount = items.length): unknown => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FraudLogView', () => {
  it('loads the first page and shows the check, decision, score and reasons in words', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    expect(requestsOf(mock)).toEqual([FIRST_PAGE])
    const row = wrapper.get('[data-testid="fraud-log-row-3"]').text()
    expect(row).toContain('Order')
    expect(row).toContain('Pending review')
    expect(row).toContain('62')
    expect(row).toContain('Too many orders from one IP address')
    expect(row).toContain('Proxy, VPN or hosting IP address')
    expect(row).toContain('203.0.113.7')
    expect(row).toContain('buyer@example.test')
    expect(wrapper.get('[data-testid="fraud-log-count"]').text()).toContain('1 Records Found')
  })

  it('links the client and the order to their screens', async () => {
    stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    const hrefs = wrapper.get('[data-testid="fraud-log-row-3"]').findAll('a').map((link) => link.attributes('href'))
    expect(hrefs).toEqual(['/clients/12', '/orders/40'])
  })

  it('shows a check with no client, order, address or reasons without links', async () => {
    stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([clean])) })
    const { wrapper } = await mountScreen(FraudLogView)

    const row = wrapper.get('[data-testid="fraud-log-row-4"]')
    expect(row.text()).toContain('Registration')
    expect(row.text()).toContain('Approved')
    expect(row.text()).toContain('None')
    expect(row.text()).toContain('N/A')
    expect(row.findAll('a')).toHaveLength(0)
  })

  it('shows the empty state when the log has no entries', async () => {
    stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudLogView)

    expect(wrapper.get('[data-testid="fraud-log-table"]').text()).toContain('No Records Found')
    expect(wrapper.get('[data-testid="fraud-log-count"]').text()).toContain('0 Records Found')
  })

  it('reloads the first page with the decision picked and again with it cleared', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    await pickOption(wrapper, 'fraud-log-decision', 'Rejected')
    await flushPromises()
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=1&pageSize=20&decision=Rejected'])

    await pickOption(wrapper, 'fraud-log-decision', 'Any')
    await flushPromises()
    expect(requestsOf(mock).slice(-1)).toEqual([FIRST_PAGE])
  })

  it('reloads the first page with the reason picked, keeping the decision', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    await pickOption(wrapper, 'fraud-log-decision', 'Pending review')
    await flushPromises()
    await pickOption(wrapper, 'fraud-log-reason', 'Proxy, VPN or hosting IP address')
    await flushPromises()

    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=1&pageSize=20&decision=Review&reason=AnonymousIp'])
  })

  it('keeps the filters when another page is opened', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held], 25)) })
    const { wrapper } = await mountScreen(FraudLogView)
    await pickOption(wrapper, 'fraud-log-decision', 'Rejected')
    await flushPromises()

    await wrapper.get('[data-testid="fraud-log-pagination-page-2"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=2&pageSize=20&decision=Rejected'])
  })

  it('loads again from the Retry of a refused load', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => problem('Not allowed.', 403) })
    const { wrapper } = await mountScreen(FraudLogView)
    const before = mock.mock.calls.length

    expect(wrapper.get('[data-testid="fraud-log-error"]').text()).toContain('Not allowed.')
    await wrapper.get('[data-testid="fraud-log-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.length).toBeGreaterThan(before)
  })
})
