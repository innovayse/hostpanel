/**
 * Tests for the order line rules: a fulfilment error code reads as a known sentence key, the
 * interrupted and service conflict codes included, or as the generic one. Whether a line may be
 * retried or closed is the API's answer on the line and is covered by the order detail tests.
 */
import { describe, expect, it } from 'vitest'
import { orderLineErrorKey } from '@/lib/orderLines'

describe('orderLineErrorKey', () => {
  it('names the sentence of a known code', () => {
    expect(orderLineErrorKey('DOMAIN_ALREADY_REGISTERED')).toBe('orders.lineErrors.domainAlreadyRegistered')
  })

  it('names its own sentence for an interrupted attempt and for a service conflict', () => {
    expect(orderLineErrorKey('ORDER_LINE_INTERRUPTED')).toBe('orders.lineErrors.orderLineInterrupted')
    expect(orderLineErrorKey('ORDER_LINE_SERVICE_CONFLICT')).toBe('orders.lineErrors.orderLineServiceConflict')
  })

  it('falls back to the generic sentence for an unknown code', () => {
    expect(orderLineErrorKey('SOMETHING_NEW')).toBe('orders.lineErrors.generic')
  })
})
