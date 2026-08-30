/**
 * Tests for rendering a money amount without inventing the currency it is in.
 *
 * The anchor case was the production one: `/client/services/16` showed `24000` and the dashboard
 * showed `20000`, because `server/api/portal/client/me.get.ts` hardcoded `currency: undefined,
 * currencyprefix: '', currencysuffix: ''` in both of its branches and threw away the ISO 4217
 * code `ClientDto.Currency` had been sending all along.
 *
 * That BFF literal is now fixed and the code reaches this file as `store.user.currency`, so the
 * symbol-bearing path below is the one a configured account takes. The empty-currency path is
 * kept as a first-class case rather than demoted to an edge case: an account with no currency
 * set is still a real account, and rendering a grouped number with no symbol is still the right
 * answer for it — a wrong symbol on a bill is worse than none, because a reader believes it.
 *
 * The assertions avoid pinning exact separator characters where a platform's CLDR data could
 * reasonably differ — Russian and Armenian group with a space whose exact codepoint has moved
 * between ICU versions. What is asserted is the part a bill depends on: the digits are grouped,
 * the decimals are there, and **no symbol is invented**.
 *
 * @module utils/formatCurrency.test
 */

import { describe, expect, it } from 'vitest'
import { EMPTY_AMOUNT, formatCurrency } from './formatCurrency'

/** The billing amount production shows for service 16, as the API sends it. */
const PRODUCTION_AMOUNT = '24000'

/** Digits only, with the grouping and decimal marks stripped, whatever they are locally. */
const digitsOf = (formatted: string): string => formatted.replace(/[^0-9]/g, '')

describe('formatCurrency', () => {
  it('groups and gives decimals to the production amount that renders bare today', () => {
    expect(formatCurrency(PRODUCTION_AMOUNT, { locale: 'en' })).toBe('24,000.00')
    expect(formatCurrency('20000', { locale: 'en' })).toBe('20,000.00')
  })

  it('invents no symbol when the API sent none', () => {
    // The whole point of the file. A wrong currency symbol on a bill is worse than none,
    // because a reader believes it. `me.get.ts` sends empty strings, so this is production.
    const rendered = formatCurrency(PRODUCTION_AMOUNT, { prefix: '', suffix: '', locale: 'hy' })

    expect(rendered).not.toMatch(/[֏$€₽]/)
    expect(rendered).not.toMatch(/[A-Za-z]/)
    expect(digitsOf(rendered)).toBe('2400000')
  })

  it('groups in the reader\'s locale', () => {
    const en = formatCurrency(PRODUCTION_AMOUNT, { locale: 'en' })
    const ru = formatCurrency(PRODUCTION_AMOUNT, { locale: 'ru' })

    // Same digits, different marks. English groups with a comma and points the decimal;
    // Russian groups with a space and commas the decimal.
    expect(digitsOf(en)).toBe(digitsOf(ru))
    expect(en).toBe('24,000.00')
    expect(ru).not.toContain(',000')
    expect(ru).toMatch(/000,00$/)
  })

  it('uses an ISO 4217 code when the API supplies one', () => {
    // Only reachable once the BFF forwards `ClientDto.Currency`; the presentation half is
    // ready for it. The placement is `Intl`'s call, not a table in this file — which is the
    // point of delegating rather than concatenating a symbol.
    expect(formatCurrency('9.99', { code: 'USD', locale: 'en' })).toBe('$9.99')

    // The same code renders differently per locale, and that is `Intl` doing its job: an
    // English reader gets the unambiguous `AMD`, an Armenian reader the native `֏`. A
    // hand-rolled prefix table cannot express that, which is the argument for delegating.
    expect(formatCurrency(PRODUCTION_AMOUNT, { code: 'AMD', locale: 'en' })).toContain('AMD')
    expect(formatCurrency(PRODUCTION_AMOUNT, { code: 'AMD', locale: 'hy' })).toContain('֏')
    expect(digitsOf(formatCurrency(PRODUCTION_AMOUNT, { code: 'AMD', locale: 'en' })))
      .toBe('2400000')
  })

  it('accepts a lowercase code, since the backend field is a free string', () => {
    expect(formatCurrency('9.99', { code: 'usd', locale: 'en' }))
      .toBe(formatCurrency('9.99', { code: 'USD', locale: 'en' }))
  })

  it('falls back to a grouped number when the code is not shaped like ISO 4217', () => {
    // Losing the amount over a malformed code would be a worse failure than dropping the
    // symbol, so a bad code degrades to the no-currency rendering rather than throwing.
    expect(formatCurrency(PRODUCTION_AMOUNT, { code: 'dollars', locale: 'en' })).toBe('24,000.00')
    expect(formatCurrency(PRODUCTION_AMOUNT, { code: '', locale: 'en' })).toBe('24,000.00')

    // `ZZZ` is deliberately *not* asserted as a fallback: it is a real, reserved ISO 4217 code
    // and `Intl` renders it as one. Anything three letters long is a code as far as `Intl` is
    // concerned — which is precisely why this frontend must never make one up.
    expect(formatCurrency(PRODUCTION_AMOUNT, { code: 'ZZZ', locale: 'en' })).toContain('ZZZ')
  })

  it('wraps the operator\'s own symbols when there is a prefix or suffix but no code', () => {
    expect(formatCurrency(PRODUCTION_AMOUNT, { prefix: '$', locale: 'en' })).toBe('$24,000.00')
    expect(formatCurrency(PRODUCTION_AMOUNT, { suffix: ' AMD', locale: 'en' }))
      .toBe('24,000.00 AMD')
  })

  it('renders an absent or unparseable amount as the em dash rather than as zero', () => {
    // Not zero: an account with no figure and an account that owes nothing are different
    // statements, and only one of them is safe to make.
    for (const absent of ['', null, undefined, 'n/a']) {
      expect(formatCurrency(absent, { locale: 'en' })).toBe(EMPTY_AMOUNT)
    }
  })

  it('renders a genuine zero as a zero', () => {
    expect(formatCurrency(0, { locale: 'en' })).toBe('0.00')
    expect(formatCurrency('0.00', { locale: 'en' })).toBe('0.00')
  })

  it('formats a negative amount, which a credited invoice line can be', () => {
    expect(digitsOf(formatCurrency('-150.5', { locale: 'en' }))).toBe('15050')
    expect(formatCurrency('-150.5', { locale: 'en' })).toContain('-')
  })

  it('falls back to English grouping outside a Nuxt render rather than throwing', () => {
    // `useNuxtApp()` is a Nuxt auto-import and does not exist here.
    expect(formatCurrency(PRODUCTION_AMOUNT)).toBe('24,000.00')
  })
})
