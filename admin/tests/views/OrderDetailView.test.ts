/**
 * Mount tests for OrderDetailView: the order, its invoice and items render on their tabs, the actions follow
 * the status, each command shows its outcome above the order, cancel and delete are confirmed in a
 * danger prompt, a confirmed delete returns to the list and a refused load offers a retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import OrderDetailView from '@/views/OrderDetailView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { BillingCycle } from '@/types/service'

/** A pending order with one line and a linked invoice. */
const pendingOrder = {
  id: 8,
  orderNumber: 'ORD-0008',
  clientId: 5,
  clientName: 'Ada Lovelace',
  status: 'Pending',
  paymentMethod: 'Example Gateway',
  total: 30,
  currency: 'EUR',
  invoiceId: 41,
  invoiceStatus: 'Unpaid',
  invoiceTotal: 33,
  itemCount: 1,
  createdAt: '2026-03-04T10:00:00Z',
  ipAddress: '10.0.0.1',
  fraudScore: null,
  fraudReasons: [],
  notes: 'Call the client',
  items: [
    {
      id: 1,
      productId: 3,
      productName: 'Shared Hosting',
      billingCycle: BillingCycle.Monthly,
      domain: 'example.com',
      hostname: null,
      firstPaymentAmount: 30,
      recurringAmount: 10,
      status: 'Pending',
      fulfilmentState: 'Pending',
      fulfilmentErrorCode: null,
      fulfilmentAttempts: 0,
      lastFulfilmentAttemptAt: null,
      canRetry: false,
      canClose: false,
    },
  ],
}

/** An accepted order with one fulfilled line and one that failed twice, ready for a retry. */
const incompleteOrder = {
  ...pendingOrder,
  status: 'Incomplete',
  items: [
    { ...pendingOrder.items[0], status: 'Active', fulfilmentState: 'Fulfilled', fulfilmentAttempts: 1, lastFulfilmentAttemptAt: '2026-03-04T10:05:00Z' },
    {
      ...pendingOrder.items[0],
      id: 2,
      productName: 'Domain',
      status: 'Active',
      fulfilmentState: 'Failed',
      fulfilmentErrorCode: 'DOMAIN_ALREADY_REGISTERED',
      fulfilmentAttempts: 2,
      lastFulfilmentAttemptAt: '2026-03-04T10:10:00Z',
      canRetry: true,
      canClose: true,
    },
  ],
}

/**
 * Mounts the detail for order 8 and waits for its load.
 *
 * @returns The wrapper and the router.
 */
const mountDetail = async () => {
  const mounted = await mountRouted(OrderDetailView, '/orders/:id', '/orders/8')
  await flushPromises()
  return mounted
}

/**
 * Opens a tab of the detail.
 *
 * @param wrapper - The mounted detail.
 * @param tab - The tab to open.
 */
const openTab = async (wrapper: VueWrapper, tab: 'overview' | 'items' | 'notes') => {
  await wrapper.get(`[data-testid="order-tab-${tab}"]`).trigger('click')
  await flushPromises()
}

describe('OrderDetailView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws the real page structure while the order loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountDetail()

    expect(wrapper.find('[data-testid="order-header-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="order-tab-items"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Order Info')
    expect(wrapper.findAll('[data-testid="order-fact-loading"]').length).toBeGreaterThan(0)
  })

  it('renders the order number, status, invoice link, items and notes', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(pendingOrder) })
    const { wrapper } = await mountDetail()

    expect(wrapper.get('[data-testid="order-header"]').text()).toContain('ORD-0008')
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Pending')
    expect(wrapper.get('[data-testid="order-invoice-link"]').attributes('href')).toBe('/billing/41')
    expect(wrapper.text()).toContain('30.00 EUR')
    expect(wrapper.find('[data-testid="order-item-1"]').exists()).toBe(false)

    await openTab(wrapper, 'items')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('Shared Hosting')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('Monthly')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('30.00 EUR')

    await openTab(wrapper, 'notes')
    expect((wrapper.get('[data-testid="order-notes-input"]').element as HTMLTextAreaElement).value).toBe('Call the client')
  })

  it('hides accept, cancel and delete on an active order', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json({ ...pendingOrder, status: 'Active' }) })
    const { wrapper } = await mountDetail()

    expect(wrapper.find('[data-testid="order-accept"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-cancel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-delete"]').exists()).toBe(false)
  })

  it('shows the API sentence above the order when accept is refused', async () => {
    stubRoutes({
      'POST /api/orders/8/accept': () => problem('The invoice is not paid.', 409),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-accept"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('The invoice is not paid.')
    expect(wrapper.find('[data-testid="order-header"]').exists()).toBe(true)
  })

  it('confirms an accepted order above it', async () => {
    stubRoutes({
      'POST /api/orders/8/accept': () => json({ failedLines: [] }),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-accept"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('Order accepted.')
  })

  it('names the lines an accepted order could not fulfil', async () => {
    stubRoutes({
      'POST /api/orders/8/accept': () =>
        json({
          failedLines: [
            {
              itemId: 1,
              kind: 'Domain',
              productName: 'Domain',
              domain: 'taken.example',
              firstPaymentAmount: 10,
              errorCode: 'DOMAIN_ALREADY_REGISTERED',
              reason: 'Taken.',
            },
          ],
        }),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-accept"]').trigger('click')
    await flushPromises()

    const feedback = wrapper.get('[data-testid="order-action-feedback"]').text()
    expect(feedback).toContain('could not be fulfilled')
    expect(feedback).toContain('taken.example')
  })

  it('shows the lines subtotal apart from the taxed invoice total', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(pendingOrder) })
    const { wrapper } = await mountDetail()

    expect(wrapper.get('[data-testid="order-lines-subtotal"]').text()).toContain('30.00 EUR')
    expect(wrapper.get('[data-testid="order-invoice-total"]').text()).toContain('33.00 EUR')
  })

  it('names the product bundle the order was placed from, and shows none for a plain order', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json({ ...pendingOrder, bundleId: 4, bundleName: 'Starter pack' }) })
    const bundled = await mountDetail()

    expect(bundled.wrapper.get('[data-testid="order-bundle"]').text()).toBe('Starter pack')

    stubRoutes({ 'GET /api/orders/8': () => json({ ...pendingOrder, bundleId: null, bundleName: null }) })
    const plain = await mountDetail()

    expect(plain.wrapper.find('[data-testid="order-bundle"]').exists()).toBe(false)
  })

  it('shows a dash for an invoice total the API could not read', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json({ ...pendingOrder, invoiceTotal: null }) })
    const { wrapper } = await mountDetail()

    expect(wrapper.get('[data-testid="order-invoice-total"]').text()).toBe('—')
  })

  it('cancels the order only after the danger confirmation', async () => {
    const mock = stubRoutes({
      'POST /api/orders/8/cancel': () => json({}),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-cancel"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/cancel'))).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/api/orders/8/cancel'))).toBe(true)
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('Order cancelled.')
  })

  it('shows the API sentence when the cancel is refused', async () => {
    stubRoutes({
      'POST /api/orders/8/cancel': () => problem('The order is already active.', 409),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-cancel"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('The order is already active.')
  })

  it('keeps the order when the cancel confirmation is dismissed', async () => {
    const mock = stubRoutes({ 'GET /api/orders/8': () => json(pendingOrder) })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-cancel"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/cancel'))).toBe(false)
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('asks before deleting, then deletes and returns to the list', async () => {
    const mock = stubRoutes({
      'DELETE /api/orders/8': () => json({}),
      'GET /api/orders/8': () => json(pendingOrder),
      'GET /api/orders?': () => json({ items: [], totalCount: 0 }),
    })
    const { wrapper, router } = await mountDetail()

    await wrapper.get('[data-testid="order-delete"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/orders')
  })

  it('stays on the order and shows the sentence when delete is refused', async () => {
    stubRoutes({
      'DELETE /api/orders/8': () => problem('The order has an invoice.', 409),
      'GET /api/orders/8': () => json(pendingOrder),
    })
    const { wrapper, router } = await mountDetail()

    await wrapper.get('[data-testid="order-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/orders/8')
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('The order has an invoice.')
  })

  it('shows the API sentence when the order cannot be loaded', async () => {
    stubRoutes({ 'GET /api/orders/8': () => problem('No such order.', 404) })
    const { wrapper } = await mountDetail()

    expect(wrapper.get('[data-testid="order-error"]').text()).toContain('No such order.')
  })

  it('shows each line fulfilment state, and a retry only on the failed one', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(incompleteOrder) })
    const { wrapper } = await mountDetail()
    await openTab(wrapper, 'items')

    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Incomplete')
    expect(wrapper.get('[data-testid="order-item-fulfilment-1"]').text()).toContain('Fulfilled')
    expect(wrapper.get('[data-testid="order-item-fulfilment-2"]').text()).toContain('Failed')
    expect(wrapper.get('[data-testid="order-item-error-2"]').text()).toContain('The domain is already registered.')
    expect(wrapper.get('[data-testid="order-item-error-2"]').text()).not.toContain('DOMAIN_ALREADY_REGISTERED')
    expect(wrapper.get('[data-testid="order-item-attempts-2"]').text()).toContain('Attempts: 2')
    expect(wrapper.find('[data-testid="order-item-retry-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-item-retry-2"]').exists()).toBe(true)
  })

  it('retries a failed line and confirms it above the order', async () => {
    const fulfilled = { ...incompleteOrder, status: 'Active', items: [incompleteOrder.items[0], { ...incompleteOrder.items[1], fulfilmentState: 'Fulfilled', fulfilmentErrorCode: null, canRetry: false, canClose: false }] }
    let reads = 0
    const mock = stubRoutes({
      'POST /api/orders/8/items/2/retry': () => json({ failedLines: [] }),
      'GET /api/orders/8': () => {
        reads += 1
        return json(reads === 1 ? incompleteOrder : fulfilled)
      },
    })
    const { wrapper } = await mountDetail()
    await openTab(wrapper, 'items')

    await wrapper.get('[data-testid="order-item-retry-2"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => (url as string).endsWith('/api/orders/8/items/2/retry'))).toBe(true)
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('Line retried and fulfilled.')
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Active')
    expect(wrapper.find('[data-testid="order-item-retry-2"]').exists()).toBe(false)
  })

  it('names the line that failed again after a retry', async () => {
    stubRoutes({
      'POST /api/orders/8/items/2/retry': () =>
        json({
          failedLines: [
            { itemId: 2, kind: 'Domain', productName: 'Domain', domain: 'taken.example', firstPaymentAmount: 10, errorCode: 'DOMAIN_ALREADY_REGISTERED', reason: 'Taken.' },
          ],
        }),
      'GET /api/orders/8': () => json(incompleteOrder),
    })
    const { wrapper } = await mountDetail()
    await openTab(wrapper, 'items')

    await wrapper.get('[data-testid="order-item-retry-2"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('could not be fulfilled again')
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('taken.example')
  })

  it('shows the API sentence when the retry is refused', async () => {
    stubRoutes({
      'POST /api/orders/8/items/2/retry': () => problem('This order line cannot be retried now.', 409),
      'GET /api/orders/8': () => json(incompleteOrder),
    })
    const { wrapper } = await mountDetail()
    await openTab(wrapper, 'items')

    await wrapper.get('[data-testid="order-item-retry-2"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('cannot be retried now')
  })

  it('loads the order again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/orders/8': () => {
        calls += 1
        return calls === 1 ? problem('No such order.', 404) : json(pendingOrder)
      },
    })
    const { wrapper } = await mountDetail()

    await wrapper.get('[data-testid="order-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-header"]').text()).toContain('ORD-0008')
  })
})
