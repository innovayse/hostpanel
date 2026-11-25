/**
 * Tests for the date and text helpers in `lib/format`: the date-only and date-time formatters
 * fall back on empty or implausible input, `toDateInputValue` cuts the day.
 */
import { describe, expect, it } from 'vitest'
import { formatCompactNumber, formatDate, formatDateTime, formatClock, formatDateTimeCompact, formatDecimal, formatElapsed, formatMonthDay, formatRelativeFromNow, formatShare, formatSizeBytes, formatSizeMb, initialsOf, EMPTY_VALUE, formatTime, joinParts, toDateInputValue } from '@/lib/format'

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
})

describe('joinParts', () => {
  it('joins the present segments with the separator', () => {
    expect(joinParts(['Online', '15 accounts', '7.4.2'])).toBe('Online · 15 accounts · 7.4.2')
  })

  it('skips missing and blank segments so no separator dangles', () => {
    expect(joinParts(['Online', null, undefined, '  ', '7.4.2'])).toBe('Online · 7.4.2')
    expect(joinParts(['Online', ''])).toBe('Online')
    expect(joinParts([null, ''])).toBe('')
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

  it('words the days in the locale it is given, not in English', () => {
    expect(formatRelativeFromNow(ago(3 * 86400), 'hy', NOW)).toBe('3 օր առաջ')
    expect(formatRelativeFromNow(ago(3 * 86400), 'hy', NOW)).not.toContain('ago')
  })

  it('counts days beyond that', () => {
    expect(formatRelativeFromNow(ago(2 * 86400), 'en', NOW)).toBe('2 days ago')
  })

  it('treats a future timestamp as now rather than a negative span', () => {
    expect(formatRelativeFromNow(new Date(NOW + 60000).toISOString(), 'en', NOW)).toBe('now')
  })

  it('words the span in Armenian for the hy locale', () => {
    expect(formatRelativeFromNow(ago(23 * 3600), 'hy', NOW)).toMatch(/[Ա-֏]/)
    expect(formatRelativeFromNow(ago(23 * 3600), 'hy', NOW)).not.toContain('hours')
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

describe('formatTime', () => {
  it('writes the time of day without seconds in the viewer zone', () => {
    expect(formatTime(new Date(2026, 9, 5, 8, 7, 9).getTime(), 'en-GB')).toBe('08:07')
  })

  it('answers an empty string for a missing or unreadable moment', () => {
    expect(formatTime(undefined, 'en-GB')).toBe('')
    expect(formatTime(Number.NaN, 'en-GB')).toBe('')
  })
})

describe('formatMonthDay', () => {
  it('gives the short month and day in the language asked, whatever the time zone', () => {
    expect(formatMonthDay('2026-03-07', 'en')).toBe('Mar 7')
    expect(formatMonthDay('2026-03-07', 'ru')).not.toBe('Mar 7')
  })

  it('returns the input when it is not a day', () => {
    expect(formatMonthDay('soon', 'en')).toBe('soon')
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

describe('initialsOf', () => {
  it('takes the first letter of each of the first two words, in upper case', () => {
    expect(initialsOf('ada lovelace')).toBe('AL')
    expect(initialsOf('Ada Byron King')).toBe('AB')
  })

  it('takes two letters of a single word, such as an email address', () => {
    expect(initialsOf('admin@example.test')).toBe('AD')
  })

  it('ignores surrounding space and is empty for a blank name', () => {
    expect(initialsOf('  Ada   Lovelace ')).toBe('AL')
    expect(initialsOf('   ')).toBe('')
  })
})

describe('EMPTY_VALUE', () => {
  it('is the em dash every formatter falls back to', () => {
    expect(EMPTY_VALUE).toBe('—')
    expect(formatDate(null)).toBe(EMPTY_VALUE)
    expect(formatDateTime(undefined)).toBe(EMPTY_VALUE)
    expect(formatDateTimeCompact('')).toBe(EMPTY_VALUE)
    expect(formatElapsed('not a date')).toBe(EMPTY_VALUE)
  })
})

describe('formatSizeMb', () => {
  it('keeps megabytes below a gigabyte', () => {
    expect(formatSizeMb(0, 'en')).toBe('0 MB')
    expect(formatSizeMb(512, 'en')).toBe('512 MB')
    expect(formatSizeMb(1023, 'en')).toBe('1,023 MB')
  })

  it('moves up to gigabytes and terabytes with at most one decimal', () => {
    expect(formatSizeMb(1024, 'en')).toBe('1 GB')
    expect(formatSizeMb(1536, 'en')).toBe('1.5 GB')
    expect(formatSizeMb(2 * 1024 * 1024, 'en')).toBe('2 TB')
  })

  it('stays in terabytes for a very large size', () => {
    expect(formatSizeMb(2048 * 1024 * 1024, 'en')).toBe('2,048 TB')
  })
})

describe('formatSizeBytes', () => {
  it('keeps bytes below a kilobyte', () => {
    expect(formatSizeBytes(0, 'en')).toBe('0 B')
    expect(formatSizeBytes(512, 'en')).toBe('512 B')
    expect(formatSizeBytes(1023, 'en')).toBe('1,023 B')
  })

  it('moves up through kilobytes and megabytes with at most one decimal', () => {
    expect(formatSizeBytes(1024, 'en')).toBe('1 KB')
    expect(formatSizeBytes(1536, 'en')).toBe('1.5 KB')
    expect(formatSizeBytes(5 * 1024 * 1024, 'en')).toBe('5 MB')
  })

  it('stays in gigabytes for a very large file', () => {
    expect(formatSizeBytes(2048 * 1024 * 1024 * 1024, 'en')).toBe('2,048 GB')
  })
})

describe('formatCompactNumber', () => {
  it('keeps a small count whole and shortens a large one in the locale', () => {
    expect(formatCompactNumber(27, 'en')).toBe('27')
    expect(formatCompactNumber(1234, 'en')).toBe('1.2K')
    expect(formatCompactNumber(2500000, 'en')).toBe('2.5M')
  })

  it('writes the short form the locale uses', () => {
    expect(formatCompactNumber(1500, 'de')).not.toBe(formatCompactNumber(1500, 'en'))
  })
})

describe('formatDecimal', () => {
  it('keeps the fixed count of fraction digits, one by default', () => {
    expect(formatDecimal(2, 'en')).toBe('2.0')
    expect(formatDecimal(2.456, 'en')).toBe('2.5')
    expect(formatDecimal(9.5, 'en', 2)).toBe('9.50')
  })
})
