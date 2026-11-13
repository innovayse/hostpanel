/**
 * Mount tests for OrdersListView: the table renders what the API sent, a pending order can be
 * accepted (and the list reloads), a refused load or command shows the API's sentence, and the status
 * filter and the page live in the address.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import OrdersListView from '@/views/OrdersListView.vue'
import { OrderStatus } from '@/types/order'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'

/** One pending order as the list endpoint returns it. */
const pendingOrder = {
  id: 1,
  orderNumber: 'ORD-0001',
  clientId: 5,
  clientName: 'Ada Lovelace',
  status: OrderStatus.Pending,
  paymentMethod: 'Example Gateway',
  total: 12.5,
  currency: 'EUR',
  invoiceId: null,
  itemCount: 2,
  failedLineCount: 0,
  ipAddress: '10.0.0.1',
  fraudScore: null,
  fraudReasons: [],
  createdAt: '2026-03-04T10:00:00Z',
}

/** One active order, which offers no accept or cancel. */
const activeOrder = { ...pendingOrder, id: 2, orderNumber: 'ORD-0002', status: OrderStatus.Active }

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(OrdersListView, '/orders', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the list at its plain address and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => (await mountListAt('/orders')).wrapper

/**
 * Chooses a status in the filter dialog and applies it.
 *
 * @param wrapper - The mounted list.
 * @param status - The status to choose.
 */
const applyStatus = async (wrapper: VueWrapper, status: OrderStatus) => {
  await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
  await flushPromises()
  wrapper
    .findAllComponents(UiSelect)
    .find((select) => select.attributes('data-testid') === 'orders-filter-status')
    ?.vm.$emit('update:modelValue', status)
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}

describe('OrdersListView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row per order with its status label and total', async () => {
    stubRoutes({ 'GET /api/orders': () => json({ items: [pendingOrder, activeOrder], totalCount: 2 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-row-1"]').text()).toContain('ORD-0001')
    expect(wrapper.get('[data-testid="orders-row-1"]').text()).toContain('Ada Lovelace')
    expect(wrapper.get('[data-testid="orders-row-1"]').text()).toContain('12.50 EUR')
    // A settled order has one action, the detail link, shown as a real link without a menu.
    expect(wrapper.get('[data-testid="orders-view-2"]').attributes('href')).toBe('/orders/2')
    // A pending order has several, so its menu is opened first.
    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    expect(wrapper.get('[data-testid="orders-view-1"]').attributes('href')).toBe('/orders/1')
  })

  it('flags an incomplete order with the number of lines that failed', async () => {
    const incomplete = { ...activeOrder, id: 3, orderNumber: 'ORD-0003', status: OrderStatus.Incomplete, failedLineCount: 2 }
    stubRoutes({ 'GET /api/orders': () => json({ items: [activeOrder, incomplete], totalCount: 2 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-row-3"]').text()).toContain('Incomplete')
    expect(wrapper.get('[data-testid="orders-failed-lines-3"]').text()).toContain('2 to retry')
    expect(wrapper.find('[data-testid="orders-failed-lines-2"]').exists()).toBe(false)
  })

  it('marks an incomplete order with no counted lines as needing attention', async () => {
    const stale = { ...activeOrder, id: 4, orderNumber: 'ORD-0004', status: OrderStatus.Incomplete, failedLineCount: 0 }
    stubRoutes({ 'GET /api/orders': () => json({ items: [stale], totalCount: 1 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-row-4"]').text()).toContain('Incomplete')
    expect(wrapper.get('[data-testid="orders-needs-attention-4"]').text()).toContain('Needs attention')
    expect(wrapper.find('[data-testid="orders-failed-lines-4"]').exists()).toBe(false)
  })

  it('prints each order total in the currency of that order', async () => {
    stubRoutes({
      'GET /api/orders': () => json({ items: [pendingOrder, { ...activeOrder, total: 4800, currency: 'AMD' }], totalCount: 2 }),
    })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-row-1"]').text()).toContain('12.50 EUR')
    expect(wrapper.get('[data-testid="orders-row-2"]').text()).toContain('4800.00 AMD')
  })

  it('offers accept and cancel only on a pending order', async () => {
    stubRoutes({ 'GET /api/orders': () => json({ items: [pendingOrder, activeOrder], totalCount: 2 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    expect(wrapper.find('[data-testid="orders-accept-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="orders-cancel-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="orders-actions-2"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="orders-accept-2"]').exists()).toBe(false)
  })

  it('accepts a pending order and reloads the list', async () => {
    const mock = stubRoutes({
      'POST /api/orders/1/accept': () => json({ failedLines: [] }),
      'GET /api/orders/1': () => json({ ...pendingOrder, status: OrderStatus.Active, items: [] }),
      'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="orders-accept-1"]').trigger('click')
    await flushPromises()

    const requests = mock.mock.calls.map(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url as string}`)
    expect(requests).toContain('POST /api/orders/1/accept')
    expect(requests.filter((line) => line.startsWith('GET /api/orders?')).length).toBe(2)
    expect(wrapper.get('[data-testid="orders-action-feedback"]').text()).toContain('Order accepted.')
  })

  it('cancels a pending order only after the danger confirmation', async () => {
    const mock = stubRoutes({
      'POST /api/orders/1/cancel': () => json({}),
      'GET /api/orders/1': () => json({ ...pendingOrder, status: OrderStatus.Cancelled, items: [] }),
      'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="orders-cancel-1"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/cancel'))).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/api/orders/1/cancel'))).toBe(true)
    expect(wrapper.get('[data-testid="orders-action-feedback"]').text()).toContain('Order cancelled.')
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('keeps the order when the cancel confirmation is dismissed', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="orders-cancel-1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/cancel'))).toBe(false)
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('shows the API sentence when a command is refused', async () => {
    stubRoutes({
      'POST /api/orders/1/accept': () => problem('Order cannot be accepted.', 409),
      'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }),
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="orders-accept-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="orders-action-feedback"]').text()).toContain('Order cannot be accepted.')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/orders': () => problem('Not allowed.', 403) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="orders-table"]').exists()).toBe(false)
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/orders': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [pendingOrder], totalCount: 1 })
      },
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="orders-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="orders-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="orders-row-1"]').text()).toContain('ORD-0001')
  })

  it('puts the applied status in the address, shows its chip and sends it', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/orders')

    await applyStatus(wrapper, OrderStatus.Fraud)

    expect(router.currentRoute.value.query).toEqual({ status: OrderStatus.Fraud })
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Fraud')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.at(-1)).toBe('/api/orders?page=1&pageSize=20&status=Fraud')
  })

  it('clears the status from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/orders?status=Fraud')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
    expect(wrapper.find('[data-testid="filter-chip-status"]').exists()).toBe(false)
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.at(-1)).toBe('/api/orders?page=1&pageSize=20')
  })

  it('sends status=Incomplete when the list is filtered to incomplete orders', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }) })
    const wrapper = await mountList()

    await applyStatus(wrapper, OrderStatus.Incomplete)

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.at(-1)).toBe('/api/orders?page=1&pageSize=20&status=Incomplete')
  })

  it('restores the status and the page from the address and sends them', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 45 }) })
    const { wrapper } = await mountListAt('/orders?status=Pending&page=2')

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls[0]).toBe('/api/orders?page=2&pageSize=20&status=Pending')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Pending')
  })

  it('moves to the next page through the address', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/orders')

    await wrapper.get('[data-testid="orders-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.at(-1)).toBe('/api/orders?page=2&pageSize=20')
  })

  it('shows the fraud score and the IP address of an order held for review, and none on an unscored one', async () => {
    const held = { ...pendingOrder, id: 5, orderNumber: 'ORD-0005', status: OrderStatus.UnderReview, fraudScore: 82, fraudReasons: [] }
    stubRoutes({ 'GET /api/orders': () => json({ items: [pendingOrder, held], totalCount: 2 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-row-5"]').text()).toContain('Pending review')
    expect(wrapper.get('[data-testid="orders-fraud-score-5"]').text()).toBe('82')
    expect(wrapper.get('[data-testid="orders-row-5"]').text()).toContain('10.0.0.1')
    expect(wrapper.find('[data-testid="orders-fraud-score-1"]').exists()).toBe(false)
  })

  it('opens the review queue from its button, filtering the list to orders pending review', async () => {
    const mock = stubRoutes({ 'GET /api/orders?': () => json({ items: [pendingOrder], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/orders')

    await wrapper.get('[data-testid="orders-review-queue"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: OrderStatus.UnderReview })
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.at(-1)).toBe('/api/orders?page=1&pageSize=20&status=UnderReview')
  })

  it('shows the empty state when there are no orders', async () => {
    stubRoutes({ 'GET /api/orders': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="orders-empty"]').text()).toContain('No orders found')
  })

  it('names the filter in the empty state of a filtered list and clears it from there', async () => {
    stubRoutes({ 'GET /api/orders?': () => json({ items: [], totalCount: 0 }) })
    const { wrapper, router } = await mountListAt('/orders?status=Fraud')

    expect(wrapper.get('[data-testid="orders-empty"]').text()).toContain('No orders match your filter')
    await wrapper.get('[data-testid="orders-clear-filters"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({})
  })
})
