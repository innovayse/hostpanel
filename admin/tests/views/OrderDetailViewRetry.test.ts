/**
 * Mount tests for the line retry on OrderDetailView: a stale in-progress line offers a retry and a
 * fresh one does not, only the retried line shows progress, a refusal appears on its own row, an
 * unknown error code reads as the generic sentence, and a retry that failed again never claims
 * success, not even for a moment, and a partly refunded line is closed through a confirmation.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import OrderDetailView from '@/views/OrderDetailView.vue'
import { OrderItemFulfilmentState, OrderItemStatus, OrderStatus } from '@/types/order'
import { BillingCycle } from '@/types/service'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'

/** Records every sentence the result banner is asked to show. */
const shown = vi.hoisted(() => [] as string[])

vi.mock('@/composables/useResultBanner', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/composables/useResultBanner')>()
  return {
    ...original,
    useResultBanner: () => {
      const banner = original.useResultBanner()
      const show = banner.show
      return {
        ...banner,
        show: (text: string, failed: boolean) => {
          shown.push(text)
          show(text, failed)
        },
      }
    },
  }
})

/**
 * Builds one line of the test order.
 *
 * @param id - Line id.
 * @param fulfilmentState - Where the line stands.
 * @param minutesAgo - How long ago the last attempt was.
 * @param fulfilmentErrorCode - The code of the last failure.
 * @returns The line as the API sends it.
 */
const line = (id: number, fulfilmentState: OrderItemFulfilmentState, minutesAgo: number, fulfilmentErrorCode: string | null = null) => ({
  id,
  productId: 3,
  productName: `Product ${id}`,
  billingCycle: BillingCycle.Monthly,
  domain: null,
  hostname: null,
  firstPaymentAmount: 10,
  recurringAmount: 10,
  status: OrderItemStatus.Active,
  fulfilmentState,
  fulfilmentErrorCode,
  fulfilmentAttempts: 1,
  lastFulfilmentAttemptAt: new Date(Date.now() - minutesAgo * 60_000).toISOString(),
})

/**
 * Builds an incomplete order with the given lines.
 *
 * @param items - The order lines.
 * @returns The order as the API sends it.
 */
const orderWith = (items: ReturnType<typeof line>[]) => ({
  id: 8,
  orderNumber: 'ORD-0008',
  clientId: 5,
  clientName: 'Ada Lovelace',
  status: OrderStatus.Incomplete,
  paymentMethod: 'Example Gateway',
  total: 30,
  currency: 'EUR',
  invoiceId: null,
  invoiceStatus: null,
  invoiceTotal: null,
  itemCount: items.length,
  failedLineCount: 0,
  createdAt: '2026-03-04T10:00:00Z',
  ipAddress: null,
  notes: null,
  items,
})

/**
 * Mounts the detail for order 8 and waits for its load.
 *
 * @returns The wrapper.
 */
const mountDetail = async () => {
  const { wrapper } = await mountRouted(OrderDetailView, '/orders/:id', '/orders/8')
  await flushPromises()
  return wrapper
}

describe('OrderDetailView line retry', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    shown.length = 0
  })

  it('offers a retry on a stale in-progress line but not on a fresh one', async () => {
    stubRoutes({
      'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.InProgress, 30), line(2, OrderItemFulfilmentState.InProgress, 1)])),
    })
    const wrapper = await mountDetail()

    expect(wrapper.find('[data-testid="order-item-retry-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="order-item-retry-2"]').exists()).toBe(false)
  })

  it('tells staff to finish the refund by hand on a partly refunded line, with no retry', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.PartiallyRefunded, 90)])) })
    const wrapper = await mountDetail()

    expect(wrapper.get('[data-testid="order-item-fulfilment-1"]').text()).toContain('Partly refunded')
    expect(wrapper.get('[data-testid="order-item-finish-refund-1"]').text()).toContain('Finish the refund by hand')
    expect(wrapper.find('[data-testid="order-item-retry-1"]').exists()).toBe(false)
  })

  it('closes a partly refunded line only after the operator confirms, then reads the order again', async () => {
    const partly = orderWith([line(1, OrderItemFulfilmentState.PartiallyRefunded, 90)])
    let closed = false
    const closeCalls: string[] = []
    stubRoutes({
      'POST /api/orders/8/items/1/close-refund': () => {
        closed = true
        closeCalls.push('close')
        return new Response(null, { status: 204 })
      },
      'GET /api/orders/8': () => json(closed ? { ...partly, status: OrderStatus.Active, items: [line(1, OrderItemFulfilmentState.Refunded, 90)] } : partly),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-close-refund-1"]').trigger('click')
    await flushPromises()
    expect(closeCalls).toEqual([])

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(closeCalls).toEqual(['close'])
    expect(shown).toContain('Line closed as refunded.')
    expect(wrapper.find('[data-testid="order-item-close-refund-1"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Active')
  })

  it('does not close the refund when the operator dismisses the prompt', async () => {
    const closeCalls: string[] = []
    stubRoutes({
      'POST /api/orders/8/items/1/close-refund': () => {
        closeCalls.push('close')
        return new Response(null, { status: 204 })
      },
      'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.PartiallyRefunded, 90)])),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-close-refund-1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(closeCalls).toEqual([])
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('shows the API refusal when the line cannot be closed', async () => {
    stubRoutes({
      'POST /api/orders/8/items/1/close-refund': () => problem('This order line cannot be refunded now.', 409),
      'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.PartiallyRefunded, 90)])),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-close-refund-1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(shown.at(-1)).toContain('cannot be refunded now')
  })

  it('offers no close-refund button on a failed or refunded line', async () => {
    stubRoutes({
      'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.Failed, 5), line(2, OrderItemFulfilmentState.Refunded, 90)])),
    })
    const wrapper = await mountDetail()

    expect(wrapper.find('[data-testid="order-item-close-refund-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="order-item-close-refund-2"]').exists()).toBe(false)
  })

  it('shows no retry on a refunded line', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.Refunded, 90)])) })
    const wrapper = await mountDetail()

    expect(wrapper.find('[data-testid="order-item-retry-1"]').exists()).toBe(false)
  })

  it('shows progress on the retried line only', async () => {
    const order = orderWith([line(1, OrderItemFulfilmentState.Failed, 5, 'PRODUCT_NOT_FOUND'), line(2, OrderItemFulfilmentState.Failed, 5, 'PRODUCT_NOT_FOUND')])
    let release: (response: Response) => void = () => undefined
    const gate = new Promise<Response>((resolve) => {
      release = resolve
    })
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => (url.endsWith('/retry') ? gate : Promise.resolve(json(order)))),
    )
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-retry-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-item-retry-1"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="order-item-retry-2"]').attributes('aria-busy')).toBeUndefined()
    release(json({ failedLines: [] }))
    await flushPromises()
    expect(wrapper.get('[data-testid="order-item-retry-1"]').attributes('aria-busy')).toBeUndefined()
  })

  it('shows the refusal on the row of the line that was retried', async () => {
    const order = orderWith([line(1, OrderItemFulfilmentState.Failed, 5), line(2, OrderItemFulfilmentState.Failed, 5)])
    stubRoutes({
      'POST /api/orders/8/items/1/retry': () => problem('This order line cannot be retried now.', 409),
      'GET /api/orders/8': () => json(order),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-retry-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-item-retry-error-1"]').text()).toContain('cannot be retried now')
    expect(wrapper.find('[data-testid="order-item-retry-error-2"]').exists()).toBe(false)
  })

  it('reads an unknown error code as the generic sentence, never as the raw code', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.Failed, 5, 'SOMETHING_NEW')])) })
    const wrapper = await mountDetail()

    const text = wrapper.get('[data-testid="order-item-error-1"]').text()
    expect(text).toContain('The line could not be fulfilled.')
    expect(text).not.toContain('SOMETHING_NEW')
  })

  it('never shows the success sentence when the retry failed again', async () => {
    stubRoutes({
      'POST /api/orders/8/items/1/retry': () =>
        json({ failedLines: [{ itemId: 1, kind: 'Domain', productName: 'Product 1', domain: 'taken.example', firstPaymentAmount: 10, errorCode: 'DOMAIN_ALREADY_REGISTERED', reason: 'Taken.' }] }),
      'GET /api/orders/8': () => json(orderWith([line(1, OrderItemFulfilmentState.Failed, 5)])),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-item-retry-1"]').trigger('click')
    await flushPromises()

    expect(shown).not.toContain('Line retried and fulfilled.')
    expect(shown.at(-1)).toContain('could not be fulfilled again')
  })
})
