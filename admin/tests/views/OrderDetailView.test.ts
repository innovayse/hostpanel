/**
 * Mount tests for OrderDetailView: the order, its invoice and items render, the actions follow
 * the status, each command shows its outcome above the order, cancel and delete are confirmed in a
 * danger prompt, a confirmed delete returns to the list and a refused load offers a retry.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
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

describe('OrderDetailView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the order number, status, invoice link, items and notes', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(pendingOrder) })
    const { wrapper } = await mountDetail()

    expect(wrapper.get('[data-testid="order-header"]').text()).toContain('ORD-0008')
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Pending')
    expect(wrapper.get('[data-testid="order-invoice-link"]').attributes('href')).toBe('/billing/41')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('Shared Hosting')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('Monthly')
    expect(wrapper.get('[data-testid="order-item-1"]').text()).toContain('30.00 EUR')
    expect(wrapper.text()).toContain('30.00 EUR')
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
