/**
 * Tests for the shared quarter option builder: grouping by year, newest first, the one-line labels with
 * short localized month names, and the value round trip.
 */
import { describe, expect, it } from 'vitest'
import { buildQuarterOptions, parseQuarterValue, quarterValue } from '@/lib/quarterOptions'

/** A moment in the middle of 2026. */
const NOW = new Date(2026, 5, 15)

describe('buildQuarterOptions', () => {
  it('offers four quarters per year, newest first, each grouped under its year', () => {
    const options = buildQuarterOptions(NOW, 2, 'en')

    expect(options.map(option => option.value)).toEqual([
      '2026-4', '2026-3', '2026-2', '2026-1', '2025-4', '2025-3', '2025-2', '2025-1',
    ])
    expect(options.map(option => option.group)).toEqual(['2026', '2026', '2026', '2026', '2025', '2025', '2025', '2025'])
  })

  it('writes the list row as quarter and months, and the trigger label with the year, on one line', () => {
    const [q4] = buildQuarterOptions(NOW, 1, 'en')

    expect(q4?.displayLabel).toBe('Q4 · Oct–Dec')
    expect(q4?.label).toBe('Q4 2026 · Oct–Dec')
    expect(q4?.label).not.toContain('\n')
  })

  it('names the months in the reader language, short', () => {
    const q1 = buildQuarterOptions(NOW, 1, 'ru').find(option => option.value === '2026-1')

    expect(q1?.displayLabel).toBe(`Q1 · ${new Intl.DateTimeFormat('ru', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(2026, 0, 1)))}–${new Intl.DateTimeFormat('ru', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(2026, 2, 1)))}`)
  })
})

describe('quarter values', () => {
  it('round-trips a quarter through its value', () => {
    expect(parseQuarterValue(quarterValue(2026, 3))).toEqual({ year: '2026', quarter: '3' })
  })

  it('answers empty parts for a malformed value', () => {
    expect(parseQuarterValue('')).toEqual({ year: '', quarter: '' })
  })
})
