/**
 * Mount tests for the fraud review on OrderDetailView: the panel shows for a scored order only, an approve refreshes the order and
 * says so, a reject asks for the reason first and sends it, and a refusal shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import OrderDetailView from '@/views/OrderDetailView.vue'
import { FraudReasonCode } from '@/types/fraud'
import { OrderStatus } from '@/types/order'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { callsOf, json, problem } from '../helpers/fetch'

/** An order held for review, with its fraud score and reasons. */
const held = {
  id: 8,
  orderNumber: 'ORD-0008',
  clientId: 5,
  clientName: 'Ada Lovelace',
  status: OrderStatus.UnderReview,
  paymentMethod: 'Example Gateway',
  total: 30,
  currency: 'EUR',
  invoiceId: null,
  invoiceStatus: null,
  invoiceTotal: null,
  itemCount: 0,
  failedLineCount: 0,
  createdAt: '2026-03-04T10:00:00Z',
  ipAddress: '203.0.113.9',
  fraudScore: 82,
  fraudReasons: [FraudReasonCode.BlockedIp, FraudReasonCode.DisposableEmail],
  notes: null,
  items: [],
}

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

/**
 * Decodes the requests a stubbed fetch received.
 *
 * @param mock - The mock returned by `stubRoutes`.
 * @returns One `METHOD url` line per request.
 */
const requestsOf = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url as string}`)

describe('OrderDetailView fraud review', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the fraud panel for a scored order and none for an unscored one', async () => {
    stubRoutes({ 'GET /api/orders/8': () => json(held) })
    const scored = await mountDetail()
    expect(scored.get('[data-testid="order-fraud-score"]').text()).toBe('82')
    expect(scored.get('[data-testid="order-fraud-reason-DisposableEmail"]').text()).toBe('Disposable e-mail address')
    expect(scored.get('[data-testid="order-status"]').text()).toContain('Pending review')

    stubRoutes({ 'GET /api/orders/8': () => json({ ...held, status: OrderStatus.Pending, fraudScore: null, fraudReasons: [] }) })
    const plain = await mountDetail()
    expect(plain.find('[data-testid="order-fraud"]').exists()).toBe(false)
  })

  it('approves the order and reads it again', async () => {
    let approved = false
    const mock = stubRoutes({
      'POST /api/orders/8/approve-review': () => {
        approved = true
        return new Response(null, { status: 204 })
      },
      'GET /api/orders/8': () => json(approved ? { ...held, status: OrderStatus.Pending } : held),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-fraud-approve"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('POST /api/orders/8/approve-review')
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('Order approved.')
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Pending')
    expect(wrapper.find('[data-testid="order-fraud-approve"]').exists()).toBe(false)
  })

  it('rejects the order only after a reason is typed, and sends that reason', async () => {
    let rejected = false
    const mock = stubRoutes({
      'POST /api/orders/8/reject-review': () => {
        rejected = true
        return new Response(null, { status: 204 })
      },
      'GET /api/orders/8': () => json(rejected ? { ...held, status: OrderStatus.Fraud } : held),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-fraud-reject"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="order-reject-submit"]').trigger('click')
    expect(requestsOf(mock).some((line) => line.startsWith('POST'))).toBe(false)

    await wrapper.get('[data-testid="order-reject-reason"]').setValue('Stolen card')
    await wrapper.get('[data-testid="order-reject-submit"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('POST /api/orders/8/reject-review')
    const post = callsOf(mock).find((call) => call.url.endsWith('/reject-review'))
    expect(post?.body).toEqual({ reason: 'Stolen card' })
    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('Order rejected.')
    expect(wrapper.get('[data-testid="order-status"]').text()).toContain('Fraud')
    expect(wrapper.find('[data-testid="order-reject-dialog"]').exists()).toBe(false)
  })

  it('shows the API sentence when the review is refused', async () => {
    stubRoutes({
      'POST /api/orders/8/approve-review': () => problem('The order is not held for review.', 409),
      'GET /api/orders/8': () => json(held),
    })
    const wrapper = await mountDetail()

    await wrapper.get('[data-testid="order-fraud-approve"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="order-action-feedback"]').text()).toContain('The order is not held for review.')
  })
})
