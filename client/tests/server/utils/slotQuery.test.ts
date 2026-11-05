/**
 * Tests for `readSlotQuery`: no context forwards nothing, a positive 32-bit service id is forwarded,
 * and a repeated, malformed or out-of-range one is a coded 400.
 *
 * @module tests/server/utils/slotQuery.test
 */

import { describe, expect, it } from 'vitest'
import { makeEvent } from '../../helpers/bffHarness'
import { readSlotQuery } from '~/server/utils/slotQuery'

describe('readSlotQuery', () => {
  it('forwards nothing when no context was sent', () => {
    expect(readSlotQuery(makeEvent())).toBe('')
  })

  it.each(['1', '42', '2147483647'])('forwards the service id %s', (serviceId) => {
    expect(readSlotQuery(makeEvent({ query: { serviceId } }))).toBe(`?serviceId=${serviceId}`)
  })

  it.each(['0', '-1', 'abc', '04', '2147483648', '12345678901'])('refuses the service id %s with a coded 400', (serviceId) => {
    expect(() => readSlotQuery(makeEvent({ query: { serviceId } }))).toThrow(expect.objectContaining({ statusCode: 400, data: { code: 'BAD_QUERY' } }))
  })

  it('refuses a repeated service id with a coded 400', () => {
    const event = makeEvent()
    // getQuery yields an array for a repeated key; the harness's typed query cannot express that, so it is set directly.
    ;(event as unknown as { query: Record<string, unknown> }).query = { serviceId: ['4', '5'] }
    expect(() => readSlotQuery(event)).toThrow(expect.objectContaining({ statusCode: 400 }))
  })
})
