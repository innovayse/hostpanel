/**
 * Tests for the money module: the dollar formatters, the shared currency symbol table, the
 * price-book labels and the report currency restatement.
 */
import { describe, expect, it } from 'vitest'
import {
  CURRENCY_CODES,
  REPORT_CURRENCY_OPTIONS,
  REPORT_CURRENCY_RATES,
  ReportCurrency,
  currencyLabel,
  baseCurrencyFormatter,
  currencySymbol,
  fixedAmount,
  formatAffixed,
  groupedAmount,
  formatCurrencyCode,
  formatCurrencyWhole,
  formatPrice,
  formatReportAmount,
  formatPlainAmount,
  formatSymbolPrice,
} from '@/lib/money'
import { CurrencyCode } from '@/types/currency'

describe('money', () => {
  it('formats a price with the currency symbol and code', () => {
    expect(formatPrice(4.99, 'USD')).toBe('$4.99 USD')
    expect(formatPrice(10, 'EUR')).toBe('€10.00 EUR')
  })

  it('shows no symbol for a currency it does not know', () => {
    expect(formatPrice(3, 'CHF')).toBe('3.00 CHF')
    expect(formatSymbolPrice(3, 'CHF')).toBe('3.00')
  })

  it('formats a price-book cell with the symbol only', () => {
    expect(formatSymbolPrice(12, 'USD')).toBe('$12.00')
    expect(formatSymbolPrice(7.5, 'EUR')).toBe('€7.50')
  })
})

describe('plain amounts with a currency code', () => {
  it('writes the number and the code of the currency it is in', () => {
    expect(formatPlainAmount(12.5, 'USD')).toBe('12.50 USD')
    expect(formatPlainAmount(53160, 'AMD')).toBe('53,160.00 AMD')
  })

  it('reads a missing code as dollars and a missing amount as zero', () => {
    expect(formatPlainAmount(3)).toBe('3.00 USD')
    expect(formatPlainAmount(undefined, null)).toBe('0.00 USD')
  })
})

describe('affixed amounts', () => {
  it('wraps the number in the prefix and suffix of a configured currency', () => {
    expect(formatAffixed(2.99, { prefix: '$', suffix: '', decimals: 2 })).toBe('$2.99')
    expect(formatAffixed(1200, { prefix: '', suffix: ' ֏', decimals: 0 })).toBe('1200 ֏')
  })
})

describe('currency symbols', () => {
  it('knows the symbol of every listed currency and labels it with the code', () => {
    for (const code of CURRENCY_CODES) {
      expect(currencySymbol(code)).not.toBe('')
    }
    expect(currencyLabel(CurrencyCode.Gbp)).toBe('GBP (£)')
  })

  it('answers an empty symbol for a code it does not know', () => {
    expect(currencySymbol('NOPE')).toBe('')
  })
})

describe('formatReportAmount', () => {
  it('shows USD with a dollar sign and two decimals by default', () => {
    expect(formatReportAmount(12)).toBe('$12.00')
  })

  it('restates the figure with the rate and symbol of the chosen currency', () => {
    expect(formatReportAmount(100, ReportCurrency.Eur)).toBe(`€${(100 * REPORT_CURRENCY_RATES.EUR).toFixed(2)}`)
    expect(formatReportAmount(1, ReportCurrency.Amd)).toBe('֏387.00')
  })

  it('appends the currency code on request', () => {
    expect(formatReportAmount(5, ReportCurrency.Usd, true)).toBe('$5.00 USD')
  })
})

describe('formatCurrencyCode', () => {
  it('formats with the symbol of a known code', () => {
    expect(formatCurrencyCode(12.5, 'USD')).toBe('$12.50')
  })

  it('falls back to the number and the code for a malformed code', () => {
    expect(formatCurrencyCode(3, 'not-a-code')).toBe('3.00 not-a-code')
  })
})

describe('baseCurrencyFormatter', () => {
  it('formats in the currency read when the amount is formatted', () => {
    let code = ''
    const format = baseCurrencyFormatter(() => code)

    code = 'USD'
    expect(format(12.5)).toBe('$12.50')
    code = 'EUR'
    expect(format(12.5)).toBe('€12.50')
  })
})

describe('formatCurrencyWhole', () => {
  it('formats a dashboard total in its currency with grouping and no decimals', () => {
    expect(formatCurrencyWhole(12500, 'USD')).toBe('$12,500')
    expect(formatCurrencyWhole(900.4, 'EUR')).toBe('€900')
  })

  it('falls back to the rounded number and the code for a malformed code', () => {
    expect(formatCurrencyWhole(12500.6, 'not-a-code')).toBe('12,501 not-a-code')
  })
})

describe('REPORT_CURRENCY_OPTIONS', () => {
  it('offers the four display currencies with a readable label', () => {
    expect(REPORT_CURRENCY_OPTIONS.map(option => option.value)).toEqual(['USD', 'EUR', 'RUB', 'AMD'])
    expect(REPORT_CURRENCY_OPTIONS[0]?.label).toContain('USD')
  })
})

describe('grouping of amounts', () => {
  it('groups thousands and keeps the decimals', () => {
    expect(groupedAmount(9300)).toBe('9,300.00')
    expect(groupedAmount(1234567.5)).toBe('1,234,567.50')
    expect(groupedAmount(12, 0)).toBe('12')
  })

  it('groups a symbol price, a plain amount, an affixed amount and a price with its code', () => {
    expect(formatSymbolPrice(9300, 'AMD')).toBe('֏9,300.00')
    expect(formatPlainAmount(9300, 'AMD')).toBe('9,300.00 AMD')
    expect(formatPrice(9300, 'AMD')).toBe('֏9,300.00 AMD')
    expect(formatAffixed(9300, { prefix: '', suffix: ' ֏', decimals: 2 })).toBe('9,300.00 ֏')
  })
})

describe('fixedAmount', () => {
  it('always shows the decimals and never groups, so an editable amount reads 23.70', () => {
    expect(fixedAmount(23.7)).toBe('23.70')
    expect(fixedAmount(1234567.5)).toBe('1234567.50')
    expect(fixedAmount(3, 0)).toBe('3')
  })
})
