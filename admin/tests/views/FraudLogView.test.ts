/**
 * Tests for FraudLogView: the log comes from the API with decisions and reasons as translated
 * labels, the client and order link to their screens, the search, the kind, decision and reason filters and the page
 * live in the address and are sent to the API, and a refused load can be retried.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import FraudLogView from '@/views/FraudLogView.vue'
import { FraudCheckKind, FraudDecision, FraudReasonCode } from '@/types/fraud'
import type { FraudLogEntry } from '@/types/fraud'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

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

/**
 * Finds one of the filter selects of the dialog.
 *
 * @param wrapper - The mounted log.
 * @param testId - The test id of the select.
 * @returns The select.
 */
const selectOf = (wrapper: VueWrapper, testId: string) => {
  const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === testId)
  if (select === undefined) {
    throw new Error(`The select ${testId} is not mounted.`)
  }
  return select
}

/**
 * Chooses values in the filter dialog and applies them.
 *
 * @param wrapper - The mounted log.
 * @param chosen - The value to pick per filter, by the test id of its select.
 */
const applyFilters = async (wrapper: VueWrapper, chosen: Record<string, string>) => {
  await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
  await flushPromises()
  for (const [testId, value] of Object.entries(chosen)) {
    selectOf(wrapper, testId).vm.$emit('update:modelValue', value)
  }
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}

/**
 * Reads the option values a mounted filter select offers, through its typed props because the select is generic.
 *
 * @param wrapper - The mounted log.
 * @param testId - The test id of the select.
 * @returns The option values, in order.
 */
const optionValues = (wrapper: VueWrapper, testId: string): string[] =>
  (selectOf(wrapper, testId).props() as unknown as { options: { value: string }[] }).options.map((option) => option.value)

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

  it('puts an applied decision in the address, shows its chip and sends it', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper, router } = await mountScreen(FraudLogView)

    await applyFilters(wrapper, { 'fraud-log-decision': FraudDecision.Rejected })

    expect(router.currentRoute.value.query).toEqual({ decision: 'Rejected' })
    expect(wrapper.get('[data-testid="filter-chip-decision"]').text()).toContain('Rejected')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=1&pageSize=20&decision=Rejected'])
  })

  it('sends the kind, the decision and the reason together', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    await applyFilters(wrapper, {
      'fraud-log-kind': FraudCheckKind.Registration,
      'fraud-log-decision': FraudDecision.Review,
      'fraud-log-reason': FraudReasonCode.AnonymousIp,
    })

    expect(requestsOf(mock).slice(-1)).toEqual([
      'GET /admin/fraud/log?page=1&pageSize=20&kind=Registration&decision=Review&reason=AnonymousIp',
    ])
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('offers any and every kind, decision and reason in the filter dialog', async () => {
    stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView)

    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
    await flushPromises()

    expect(optionValues(wrapper, 'fraud-log-kind')).toEqual(['', ...Object.values(FraudCheckKind)])
    expect(optionValues(wrapper, 'fraud-log-decision')).toEqual(['', ...Object.values(FraudDecision)])
    expect(optionValues(wrapper, 'fraud-log-reason')).toEqual(['', ...Object.values(FraudReasonCode)])
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper, router } = await mountScreen(FraudLogView)

    await wrapper.get('[data-testid="fraud-log-search"]').setValue('203.0')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: '203.0' })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=1&pageSize=20&search=203.0'])
  })

  it('restores the search and the filters from the address and shows their chips', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView, {}, '/?search=buyer&kind=Order&decision=Review&reason=OrderVelocity')

    expect(requestsOf(mock)[0]).toBe('GET /admin/fraud/log?page=1&pageSize=20&search=buyer&kind=Order&decision=Review&reason=OrderVelocity')
    expect((wrapper.get('[data-testid="fraud-log-search"]').element as HTMLInputElement).value).toBe('buyer')
    expect(wrapper.get('[data-testid="filter-chip-kind"]').text()).toContain('Order')
    expect(wrapper.get('[data-testid="filter-chip-decision"]').text()).toContain('Pending review')
    expect(wrapper.get('[data-testid="filter-chip-reason"]').text()).toContain('Too many orders from one IP address')
  })

  it('ignores a filter in the address that is not a member and sends nothing for it', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper } = await mountScreen(FraudLogView, {}, '/?decision=Nonsense')

    expect(requestsOf(mock)[0]).toBe(FIRST_PAGE)
    expect(wrapper.find('[data-testid="filter-chip-decision"]').exists()).toBe(false)
  })

  it('clears a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held])) })
    const { wrapper, router } = await mountScreen(FraudLogView, {}, '/?decision=Rejected')

    await wrapper.get('[data-testid="filter-chip-remove-decision"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
    expect(wrapper.find('[data-testid="filter-chip-decision"]').exists()).toBe(false)
    expect(requestsOf(mock).slice(-1)).toEqual([FIRST_PAGE])
  })

  it('keeps the search and the filters when another page is opened', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held], 45)) })
    const { wrapper, router } = await mountScreen(FraudLogView, {}, '/?search=buyer&decision=Rejected')

    await wrapper.get('[data-testid="fraud-log-pagination-page-2"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'buyer', decision: 'Rejected', page: '2' })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=2&pageSize=20&search=buyer&decision=Rejected'])
  })

  it('returns to the first page when a filter is applied', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([held], 45)) })
    const { wrapper, router } = await mountScreen(FraudLogView, {}, '/?page=2')
    expect(requestsOf(mock)[0]).toBe('GET /admin/fraud/log?page=2&pageSize=20')

    await applyFilters(wrapper, { 'fraud-log-kind': FraudCheckKind.Order })

    expect(router.currentRoute.value.query).toEqual({ kind: 'Order' })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/log?page=1&pageSize=20&kind=Order'])
  })

  it('names the search in the empty state of a searched log', async () => {
    stubRouteTable({ 'GET /admin/fraud/log': () => json(pageOf([])) })
    const { wrapper } = await mountScreen(FraudLogView, {}, '/?search=nobody')

    expect(wrapper.get('[data-testid="fraud-log-table"]').text()).toContain('No log entries match your search or filters')
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
