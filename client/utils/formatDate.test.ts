/**
 * Tests for rendering a date the API sent.
 *
 * The two cases that matter most are the two that shipped. `/client/services/16` printed
 * `2026-09-08T08:19:37.756135+00:00` verbatim into a description list, and the row above it
 * printed nothing at all because `services/[id]/index.get.ts` hardcodes `regdate: ''`. Both
 * are asserted here against the exact production values rather than against invented ones.
 *
 * The timezone case is the one a reader is most likely to think is over-cautious. It is not:
 * the suite runs under whatever `TZ` the machine has, so it asserts the *day* survives rather
 * than asserting a fixed string, and forces a westerly zone for the one case where a naive
 * implementation is provably wrong.
 *
 * Locale is always passed explicitly here. Outside a Nuxt render `useNuxtApp()` throws and the
 * util falls back to `en` — which is itself asserted, because that fallback is what keeps the
 * util callable from a test at all.
 *
 * @module utils/formatDate.test
 */

import { describe, expect, it } from 'vitest'
import { EMPTY_DATE, formatDate, formatDateTime } from './formatDate'

/** The `nextduedate` production serves for service 16, microseconds and offset included. */
const PRODUCTION_NEXT_DUE = '2026-09-08T08:19:37.756135+00:00'

describe('formatDate', () => {
  it('renders the production next-due-date as a date instead of a raw timestamp', () => {
    const rendered = formatDate(PRODUCTION_NEXT_DUE, 'en')

    expect(rendered).toBe('Sep 8, 2026')
    // The specific failure that shipped: no ISO punctuation, no microseconds, no offset.
    expect(rendered).not.toContain('T')
    expect(rendered).not.toContain('756135')
    expect(rendered).not.toContain('+00:00')
  })

  it('renders an empty registration date as the em dash, not as Invalid Date', () => {
    // `regdate: ''` is a literal in `server/api/portal/client/services/[id]/index.get.ts`,
    // so this path is live on every service page, not a defensive hypothetical.
    for (const absent of ['', null, undefined]) {
      expect(formatDate(absent, 'en')).toBe(EMPTY_DATE)
    }
  })

  it('renders an unparseable value as the em dash rather than echoing it', () => {
    const rendered = formatDate('not a date at all', 'en')

    expect(rendered).toBe(EMPTY_DATE)
    expect(rendered).not.toContain('Invalid')
    expect(rendered).not.toContain('not a date')
  })

  it('treats the zero-date sentinels as absent', () => {
    // MySQL's `0000-00-00` and .NET's `default(DateTime)`. Pages guarded these with scattered
    // `startsWith` checks before this util existed; the guard lives in one place now.
    expect(formatDate('0000-00-00', 'en')).toBe(EMPTY_DATE)
    expect(formatDate('0001-01-01T00:00:00Z', 'en')).toBe(EMPTY_DATE)
  })

  it('keeps the calendar day the backend wrote, in a timezone that would shift it', () => {
    const original = process.env.TZ
    // UTC-11. `new Date('2026-09-08')` is UTC midnight, so a naive implementation renders the
    // 7th here — a renewal date a day early on the screen a customer pays from.
    process.env.TZ = 'Pacific/Pago_Pago'
    try {
      expect(formatDate('2026-09-08', 'en')).toBe('Sep 8, 2026')
      expect(formatDate(PRODUCTION_NEXT_DUE, 'en')).toBe('Sep 8, 2026')
    }
    finally {
      process.env.TZ = original
    }
  })

  it('formats in the language the visitor is reading', () => {
    // Not asserting exact punctuation — date-fns may reword a locale between releases. What
    // must hold is that the three languages differ and that hy is not silently English.
    const en = formatDate(PRODUCTION_NEXT_DUE, 'en')
    const ru = formatDate(PRODUCTION_NEXT_DUE, 'ru')
    const hy = formatDate(PRODUCTION_NEXT_DUE, 'hy')

    expect(new Set([en, ru, hy]).size).toBe(3)
    expect(ru).toMatch(/[а-яё]/i)
    expect(hy).toMatch(/[԰-֏]/)
  })

  it('accepts a regional tag as well as a bare language code', () => {
    expect(formatDate(PRODUCTION_NEXT_DUE, 'hy-AM')).toBe(formatDate(PRODUCTION_NEXT_DUE, 'hy'))
  })

  it('falls back to English outside a Nuxt render rather than throwing', () => {
    // `useNuxtApp()` does not exist here — it is a Nuxt auto-import. The util must survive
    // that, or no page util is testable and no util is callable from a Nitro route.
    expect(formatDate(PRODUCTION_NEXT_DUE)).toBe(formatDate(PRODUCTION_NEXT_DUE, 'en'))
  })
})

describe('formatDateTime', () => {
  it('keeps the clock a calendar date drops', () => {
    const rendered = formatDateTime(PRODUCTION_NEXT_DUE, 'en')

    expect(rendered).toMatch(/Sep 8, 2026/)
    expect(rendered).toMatch(/\d{1,2}:\d{2}/)
    // Seconds and microseconds are deliberately not shown; nothing on these screens is
    // decided at that resolution.
    expect(rendered).not.toContain('756135')
  })

  it('shows the moment in the reader\'s own timezone, unlike a calendar date', () => {
    const original = process.env.TZ
    process.env.TZ = 'Pacific/Pago_Pago'
    try {
      // 08:19 UTC is the previous evening at UTC-11. That is correct for an instant and wrong
      // for a billing day — which is exactly why the two functions are separate.
      expect(formatDateTime(PRODUCTION_NEXT_DUE, 'en')).toMatch(/Sep 7, 2026/)
      expect(formatDate(PRODUCTION_NEXT_DUE, 'en')).toBe('Sep 8, 2026')
    }
    finally {
      process.env.TZ = original
    }
  })

  it('renders an absent or unparseable moment as the em dash', () => {
    expect(formatDateTime(null, 'en')).toBe(EMPTY_DATE)
    expect(formatDateTime('', 'en')).toBe(EMPTY_DATE)
    expect(formatDateTime('yesterday-ish', 'en')).toBe(EMPTY_DATE)
  })
})
