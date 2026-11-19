/**
 * Tests for GatewayLogView: it loads the log from the API, shows the empty state when there is none, and the search,
 * the result and the period (days) are kept in the address, sent to the API and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import GatewayLogView from '@/views/GatewayLogView.vue'
import schema from '@/views/GatewayLogView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted } from '../helpers/mount'
import { callOf, json, stubFetch, problem } from '../helpers/fetch'

/** A log page with one settled payment. */
const page = {
  items: [
    {
      id: 'tx-1',
      dateTime: '2026-10-14T09:00:00Z',
      gateway: 'bank-gateway',
      debugData: 'Invoice #5 ref abc',
      result: 'Payment',
      invoiceId: 5,
    },
  ],
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
}

/**
 * Mounts the screen at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountLogAt = async (query = '') => {
  setActivePinia(createPinia())
  const screen = await mountRouted(GatewayLogView, '/billing/gateway-log', `/billing/gateway-log${query}`)
  await flushPromises()
  return screen
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('GatewayLogView', () => {
  it('shows the empty state when the log has no entries', async () => {
    stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))
    const { wrapper } = await mountLogAt()

    expect(wrapper.text()).toContain('Gateway Transaction Log')
    expect(wrapper.get('[data-testid="billing-gateway-log-table"]').text()).toContain('No Records Found')
    expect(wrapper.get('[data-testid="billing-gateway-log-count"]').text()).toContain('0 Records Found')
  })

  it('renders the entries the API returns', async () => {
    const mock = stubFetch(json(page))
    const { wrapper } = await mountLogAt()

    expect(callOf(mock).url).toBe('/api/gateway-logs?page=1&pageSize=20')
    expect(wrapper.get('[data-testid="billing-gateway-log-table"]').text()).toContain('Invoice #5 ref abc')
    expect(wrapper.get('[data-testid="billing-gateway-log-table"]').text()).toContain('Payment')
    expect(wrapper.get('[data-testid="billing-gateway-log-count"]').text()).toContain('1 Records Found')
  })

  it('draws the headers of the schema columns, in order', async () => {
    stubFetch(json(page))
    const { wrapper } = await mountLogAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubFetch(json(page))
    const { wrapper, router } = await mountLogAt()

    await wrapper.get('[data-testid="billing-gateway-log-search"]').setValue('ref-4')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'ref-4' })
    expect(callOf(mock, mock.mock.calls.length - 1).url).toBe('/api/gateway-logs?page=1&pageSize=20&search=ref-4')
  })

  it('sends the result picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubFetch(json(page))
    const { wrapper, router } = await mountLogAt()

    await applyListFilter(wrapper, { 'billing-gateway-log-filter-result': 'Refund' }, 'billing-gateway-log-filter')

    expect(router.currentRoute.value.query).toEqual({ result: 'Refund' })
    expect(callOf(mock, mock.mock.calls.length - 1).url).toBe('/api/gateway-logs?page=1&pageSize=20&result=Refund')
    expect(wrapper.get('[data-testid="filter-chip-result"]').text()).toContain('Refund')
  })

  it('restores the search, the result, the period and the page from the address and sends the period as days', async () => {
    const mock = stubFetch(json(page))
    const { wrapper } = await mountLogAt('?search=ref&result=Pending&from=2026-03-01&to=2026-03-31&page=2')

    expect(callOf(mock).url).toBe('/api/gateway-logs?page=2&pageSize=20&search=ref&from=2026-03-01&to=2026-03-31&result=Pending')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubFetch(json(page))
    const { wrapper } = await mountLogAt('?result=Sideways&from=soon&to=2026-03-31T10%3A00%3A00Z')

    expect(callOf(mock).url).toBe('/api/gateway-logs?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubFetch(json(page))
    const { wrapper, router } = await mountLogAt('?result=Refund&from=2026-03-01')

    await wrapper.get('[data-testid="filter-chip-remove-result"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: '2026-03-01' })
    expect(callOf(mock, mock.mock.calls.length - 1).url).toBe('/api/gateway-logs?page=1&pageSize=20&from=2026-03-01')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubFetch(json({ ...page, totalCount: 60, totalPages: 3 }))
    const { wrapper, router } = await mountLogAt()

    await wrapper.get('[data-testid="billing-gateway-log-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(callOf(mock, mock.mock.calls.length - 1).url).toBe('/api/gateway-logs?page=2&pageSize=20')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const { wrapper } = await mountLogAt()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="billing-gateway-log-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
