/**
 * Tests for the dashboard helpers: the part of the day, the signed change text, and the chart day caption.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { dayPartOf, DayPart, formatChange, formatDayCaption } from '@/lib/dashboardOverview'

describe('dayPartOf', () => {
  it.each([
    [0, DayPart.Morning],
    [11, DayPart.Morning],
    [12, DayPart.Afternoon],
    [17, DayPart.Afternoon],
    [18, DayPart.Evening],
    [23, DayPart.Evening],
  ])('puts hour %i in the %s', (hour, part) => {
    expect(dayPartOf(hour)).toBe(part)
  })
})

describe('formatChange', () => {
  it('writes the sign so it reads without colour', () => {
    expect(formatChange(12.4)).toBe('+12%')
    expect(formatChange(-4.6)).toBe('-5%')
    expect(formatChange(0)).toBe('0%')
  })
})

describe('formatDayCaption', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('writes the day and the short month in the language given', () => {
    expect(formatDayCaption('2026-03-12', 'en')).toBe('Mar 12')
    expect(formatDayCaption('2026-03-12', 'ru')).toMatch(/^12 мар/)
  })

  it('writes the day first and the month from the app messages when the engine lacks the locale', () => {
    vi.spyOn(Intl.DateTimeFormat, 'supportedLocalesOf').mockReturnValue([])

    expect(formatDayCaption('2026-03-12', 'hy')).toBe('12 Մրտ')
  })

  it('does not move the day with the time zone of the machine', () => {
    expect(formatDayCaption('2026-01-01', 'en')).toBe('Jan 1')
  })
})
