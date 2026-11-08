/**
 * Tests for the order line rules: a failed line and a line left in progress past the retry lease
 * may be retried, a fresh claim and a closed line may not, and a fulfilment error code reads as a
 * known sentence key or the generic one.
 */
import { describe, expect, it } from 'vitest'
import { ORDER_LINE_RETRY_LEASE_MS } from '@/lib/limits'
import { canRetryOrderLine, orderLineErrorKey } from '@/lib/orderLines'
import { OrderItemFulfilmentState, OrderItemStatus, OrderStatus } from '@/types/order'
import type { OrderItemDetail } from '@/types/order'
import { BillingCycle } from '@/types/service'

/** The moment the rules are evaluated at. */
const NOW = Date.parse('2026-03-04T12:00:00Z')

/**
 * Builds a line in the given state.
 *
 * @param fulfilmentState - Where the line stands.
 * @param lastFulfilmentAttemptAt - When the last attempt started or ended.
 * @returns The line.
 */
const line = (fulfilmentState: OrderItemFulfilmentState, lastFulfilmentAttemptAt: string | null): OrderItemDetail => ({
  id: 1,
  productId: 3,
  productName: 'Shared Hosting',
  billingCycle: BillingCycle.Monthly,
  domain: null,
  hostname: null,
  firstPaymentAmount: 1,
  recurringAmount: 1,
  status: OrderItemStatus.Active,
  fulfilmentState,
  fulfilmentErrorCode: null,
  fulfilmentAttempts: 1,
  lastFulfilmentAttemptAt,
})

/**
 * Builds the ISO moment a number of milliseconds before NOW.
 *
 * @param ms - How long ago.
 * @returns The ISO string.
 */
const ago = (ms: number): string => new Date(NOW - ms).toISOString()

describe('canRetryOrderLine', () => {
  it('allows a failed line', () => {
    expect(canRetryOrderLine(OrderStatus.Incomplete, line(OrderItemFulfilmentState.Failed, ago(1000)), NOW)).toBe(true)
  })

  it('allows an in-progress line once its claim is older than the lease', () => {
    const stale = line(OrderItemFulfilmentState.InProgress, ago(ORDER_LINE_RETRY_LEASE_MS))
    expect(canRetryOrderLine(OrderStatus.Incomplete, stale, NOW)).toBe(true)
  })

  it('refuses an in-progress line still inside the lease, or without an attempt time', () => {
    const fresh = line(OrderItemFulfilmentState.InProgress, ago(ORDER_LINE_RETRY_LEASE_MS - 1000))
    expect(canRetryOrderLine(OrderStatus.Incomplete, fresh, NOW)).toBe(false)
    expect(canRetryOrderLine(OrderStatus.Incomplete, line(OrderItemFulfilmentState.InProgress, null), NOW)).toBe(false)
  })

  it('refuses fulfilled, pending and every refund state of a line, and any line of an order that is not incomplete', () => {
    for (const state of [OrderItemFulfilmentState.Fulfilled,
      OrderItemFulfilmentState.Pending,
      OrderItemFulfilmentState.Refunded,
      OrderItemFulfilmentState.Refunding,
      OrderItemFulfilmentState.PartiallyRefunded,
    ]) {
      expect(canRetryOrderLine(OrderStatus.Incomplete, line(state, ago(ORDER_LINE_RETRY_LEASE_MS * 2)), NOW)).toBe(false)
    }
    expect(canRetryOrderLine(OrderStatus.Active, line(OrderItemFulfilmentState.Failed, ago(1000)), NOW)).toBe(false)
  })
})

describe('orderLineErrorKey', () => {
  it('names the sentence of a known code', () => {
    expect(orderLineErrorKey('DOMAIN_ALREADY_REGISTERED')).toBe('orders.lineErrors.domainAlreadyRegistered')
  })

  it('falls back to the generic sentence for an unknown code', () => {
    expect(orderLineErrorKey('SOMETHING_NEW')).toBe('orders.lineErrors.generic')
  })
})
