/**
 * Unit tests for the quote line helpers.
 */
import { describe, expect, it } from 'vitest'
import { blankQuoteLine, quoteLineFrom, quoteLinesPayload } from '@/lib/quoteLines'

describe('quote lines', () => {
  it('gives every blank line its own key', () => {
    expect(blankQuoteLine().key).not.toBe(blankQuoteLine().key)
  })

  it('keeps the stored fields and adds a key', () => {
    const line = quoteLineFrom({ id: 4, quantity: 2, description: 'Hosting', unitPrice: 10, discountPercent: 5, taxed: true })

    expect(line).toMatchObject({ id: 4, quantity: 2, description: 'Hosting', unitPrice: 10, discountPercent: 5, taxed: true })
    expect(typeof line.key).toBe('number')
  })

  it('drops the key from the request lines', () => {
    const payload = quoteLinesPayload([blankQuoteLine()])

    expect(payload[0]).not.toHaveProperty('key')
    expect(payload[0]).toMatchObject({ quantity: 1, description: '' })
  })
})
