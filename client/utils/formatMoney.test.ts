/**
 * Tests for formatting an amount already known to be in a specific configured currency.
 *
 * @module utils/formatMoney.test
 */

import { describe, expect, it } from 'vitest'
import { formatMoney } from './formatMoney'

describe('formatMoney', () => {
  it('formats USD with two decimal places and the prefix', () => {
    expect(formatMoney(2.99, { code: 'USD', prefix: '$', suffix: '', decimals: 2 })).toBe('$2.99')
  })

  it('formats AMD with no decimal places and the suffix', () => {
    const formatted = formatMoney(1200, { code: 'AMD', prefix: '', suffix: ' ֏', decimals: 0 })
    // Grouping separator varies by ICU build; assert digits and suffix, not the exact separator.
    expect(formatted.replace(/[^0-9]/g, '')).toBe('1200')
    expect(formatted.endsWith(' ֏')).toBe(true)
    expect(formatted).not.toContain('.')
  })

  it('pins the decimal count to the currency, not the amount', () => {
    expect(formatMoney(5, { prefix: '$', decimals: 2 })).toBe('$5.00')
  })

  it('renders an em dash for a null amount', () => {
    expect(formatMoney(null, { decimals: 2 })).toBe('—')
  })

  it('renders an em dash for an undefined amount', () => {
    expect(formatMoney(undefined, { decimals: 2 })).toBe('—')
  })

  it('renders an em dash for a non-finite amount', () => {
    expect(formatMoney(Number.NaN, { decimals: 2 })).toBe('—')
  })

  it('wraps with prefix and suffix together when both are configured', () => {
    expect(formatMoney(10, { prefix: '£', suffix: ' GBP', decimals: 2 })).toBe('£10.00 GBP')
  })
})
