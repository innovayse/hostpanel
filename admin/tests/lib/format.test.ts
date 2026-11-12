/**
 * Tests for the date and text helpers in `lib/format`: the date-only and date-time formatters
 * fall back on empty or implausible input, `toDateInputValue` cuts the day, and `capitalize`
 * upper-cases the first letter.
 */
import { describe, expect, it } from 'vitest'
import { capitalize, formatDate, formatDateTime, formatClock, formatDateTimeCompact, formatElapsed, formatRelativeFromNow, formatShare, toDateInputValue } from '@/lib/format'

describe('format', () => {
  it('formats a date as day/month/year', () => {
    expect(formatDate('2026-03-04T10:00:00Z')).toBe('04/03/2026')
  })

  it('falls back for empty, invalid and placeholder dates', () => {
    expect(formatDate(null)).toBe('—')
    expect(formatDate('nonsense')).toBe('—')
    expect(formatDate('0001-01-01T00:00:00Z')).toBe('—')
    expect(formatDate(undefined, 'n/a')).toBe('n/a')
  })

  it('formats a timestamp with the time of day', () => {
    expect(formatDateTime('2026-03-04T10:30:00Z')).toMatch(/^04\/03\/2026, \d{2}:30$/)
  })

  it('falls back for an empty or placeholder timestamp', () => {
    expect(formatDateTime(null)).toBe('—')
    expect(formatDateTime('0001-01-01T00:00:00Z')).toBe('—')
  })

  it('cuts the day for a date input', () => {
    expect(toDateInputValue('2026-03-04T10:00:00Z')).toBe('2026-03-04')
    expect(toDateInputValue(null)).toBe('')
    expect(toDateInputValue('nonsense')).toBe('')
  })

  it('capitalises the first letter and falls back for an empty value', () => {
    expect(capitalize('monthly')).toBe('Monthly')
    expect(capitalize('')).toBe('—')
    expect(capitalize(null, '-')).toBe('-')
  })
})

describe('formatDateTimeCompact', () => {
  it('returns a dash for a missing or unreadable date', () => {
    expect(formatDateTimeCompact(null)).toBe('\u2014')
    expect(formatDateTimeCompact(undefined)).toBe('\u2014')
    expect(formatDateTimeCompact('')).toBe('\u2014')
    expect(formatDateTimeCompact('not a date')).toBe('\u2014')
  })

  it('formats a local date as DD/MM/YYYY HH:MM', () => {
    const local = new Date(2026, 9, 5, 8, 7)

    expect(formatDateTimeCompact(local.toISOString())).toBe('05/10/2026 08:07')
  })
})

describe('formatElapsed', () => {
  const now = new Date('2026-10-14T12:00:00Z').getTime()

  it('returns a dash for a missing or unreadable timestamp', () => {
    expect(formatElapsed(null, now)).toBe('\u2014')
    expect(formatElapsed('nope', now)).toBe('\u2014')
  })

  it('shows days, hours and minutes, dropping zero days and hours', () => {
    expect(formatElapsed('2026-10-08T10:43:00Z', now)).toBe('6d 1h 17m')
    expect(formatElapsed('2026-10-14T10:55:00Z', now)).toBe('1h 5m')
    expect(formatElapsed('2026-10-14T11:59:00Z', now)).toBe('1m')
  })

  it('never goes negative for a timestamp in the future', () => {
    expect(formatElapsed('2026-10-14T13:00:00Z', now)).toBe('0m')
  })
})

/** A fixed "now" so the wording is deterministic. */
const NOW = Date.parse('2026-10-14T12:00:00Z')

/**
 * Builds the ISO timestamp of a moment some seconds before the fixed now.
 *
 * @param seconds - How long before now.
 * @returns ISO 8601 timestamp.
 */
const ago = (seconds: number): string => new Date(NOW - seconds * 1000).toISOString()

describe('formatRelativeFromNow', () => {
  it('says "now" for a moment under a minute ago', () => {
    expect(formatRelativeFromNow(ago(20), 'en', NOW)).toBe('now')
  })

  it('counts minutes under an hour', () => {
    expect(formatRelativeFromNow(ago(5 * 60), 'en', NOW)).toBe('5 minutes ago')
  })

  it('counts hours under a day', () => {
    expect(formatRelativeFromNow(ago(3 * 3600), 'en', NOW)).toBe('3 hours ago')
  })

  it('counts days beyond that', () => {
    expect(formatRelativeFromNow(ago(2 * 86400), 'en', NOW)).toBe('2 days ago')
  })

  it('treats a future timestamp as now rather than a negative span', () => {
    expect(formatRelativeFromNow(new Date(NOW + 60000).toISOString(), 'en', NOW)).toBe('now')
  })

  it('words the span in the requested locale', () => {
    expect(formatRelativeFromNow(ago(5 * 60), 'ru', NOW)).toContain('5')
    expect(formatRelativeFromNow(ago(5 * 60), 'ru', NOW)).not.toBe('5 minutes ago')
  })
})

describe('formatClock', () => {
  it('writes the time of day with seconds in the viewer zone', () => {
    const local = new Date(2026, 9, 5, 8, 7, 9)

    expect(formatClock(local.toISOString(), 'en-GB')).toBe('08:07:09')
  })

  it('answers an empty string for an unreadable timestamp', () => {
    expect(formatClock('not a date', 'en-GB')).toBe('')
  })
})

describe('formatShare', () => {
  it('writes a share as a percentage', () => {
    expect(formatShare(0.5, 'en')).toBe('50%')
    expect(formatShare(0, 'en')).toBe('0%')
    expect(formatShare(1, 'en')).toBe('100%')
  })

  it('keeps at most one decimal', () => {
    expect(formatShare(0.1234, 'en')).toBe('12.3%')
    expect(formatShare(0.12349, 'en')).toBe('12.3%')
  })
})
