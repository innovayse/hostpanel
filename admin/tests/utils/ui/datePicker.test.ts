/**
 * Unit tests for the date helpers shared by UiDatePicker and UiDateRangePicker.
 */
import { describe, expect, it } from 'vitest'
import {
  DateRangePreset,
  dateCellSelectedClasses,
  datePresetActiveClasses,
  QuickDate,
  UiDatePickerDirection,
  buildYearOptions,
  formatDisplayDate,
  formatIsoDate,
  getDayKeysForMonth,
  getDaysForMonth,
  matchQuickDate,
  parsePartialDate,
  parseTypedDate,
  partialToDate,
  presetRange,
  quickDateFor,
  dateLibraryThemeClasses,
} from '@/utils/ui/datePicker'

/** Fixed reference day (a Wednesday) so every expectation is deterministic. */
const TODAY = new Date(2026, 4, 20)

describe('datePicker helpers', () => {
  it('formats and re-displays an ISO day', () => {
    expect(formatIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
    expect(formatDisplayDate('2026-01-05')).toBe('05/01/2026')
  })

  it('parses a complete typed date with any separators', () => {
    expect(parseTypedDate('15/03/2024')).toBe('2024-03-15')
    expect(parseTypedDate('15032024')).toBe('2024-03-15')
  })

  it('refuses an incomplete or out-of-range typed date', () => {
    expect(parseTypedDate('1503202')).toBeNull()
    expect(parseTypedDate('32012024')).toBeNull()
    expect(parseTypedDate('15132024')).toBeNull()
    expect(parseTypedDate('15031999')).toBeNull()
  })

  it('reads the parts of a partly typed date', () => {
    expect(parsePartialDate('1')).toEqual({ day: 1 })
    expect(parsePartialDate('150')).toEqual({ day: 15 })
    expect(parsePartialDate('15032')).toEqual({ day: 15, month: 3 })
    expect(parsePartialDate('15032024')).toEqual({ day: 15, month: 3, year: 2024 })
  })

  it('builds a date only from a complete partial', () => {
    expect(partialToDate({ day: 1, month: 2 })).toBeNull()
    expect(partialToDate({ day: 1, month: 2, year: 2025 })).toEqual(new Date(2025, 1, 1))
  })

  it('pads the month grid with the weekday offset', () => {
    // 1 May 2026 is a Friday: five leading blanks, then 31 days.
    const cells = getDaysForMonth(new Date(2026, 4, 1))
    expect(cells.slice(0, 5)).toEqual([null, null, null, null, null])
    expect(cells[5]).toBe(1)
    expect(cells).toHaveLength(36)
  })

  it('keys every grid cell by the ISO day it sits on', () => {
    // The five leading blanks sit on 26 to 30 April; the grid then runs 1 to 31 May.
    const keys = getDayKeysForMonth(new Date(2026, 4, 1))
    expect(keys.slice(0, 6)).toEqual(['2026-04-26', '2026-04-27', '2026-04-28', '2026-04-29', '2026-04-30', '2026-05-01'])
    expect(keys).toHaveLength(36)
    expect(new Set(keys).size).toBe(36)
  })

  it('offers the configured year span', () => {
    const years = buildYearOptions()
    expect(years[0]).toEqual({ value: '2020', label: '2020' })
    expect(years[years.length - 1]?.value).toBe('2030')
  })

  it('resolves quick choices backwards and forwards', () => {
    expect(formatIsoDate(quickDateFor(QuickDate.Day, UiDatePickerDirection.Past, TODAY))).toBe('2026-05-19')
    expect(formatIsoDate(quickDateFor(QuickDate.Week, UiDatePickerDirection.Past, TODAY))).toBe('2026-05-13')
    expect(formatIsoDate(quickDateFor(QuickDate.Month, UiDatePickerDirection.Past, TODAY))).toBe('2026-04-20')
    expect(formatIsoDate(quickDateFor(QuickDate.Year, UiDatePickerDirection.Past, TODAY))).toBe('2025-05-20')
    expect(formatIsoDate(quickDateFor(QuickDate.Day, UiDatePickerDirection.Future, TODAY))).toBe('2026-05-21')
    expect(formatIsoDate(quickDateFor(QuickDate.Week, UiDatePickerDirection.Future, TODAY))).toBe('2026-05-27')
    expect(formatIsoDate(quickDateFor(QuickDate.Year, UiDatePickerDirection.Future, TODAY))).toBe('2027-05-20')
  })

  it('names the quick choice a stored day matches', () => {
    expect(matchQuickDate(null, UiDatePickerDirection.Past, TODAY)).toBeNull()
    expect(matchQuickDate('2026-05-20', UiDatePickerDirection.Past, TODAY)).toBe(QuickDate.Today)
    expect(matchQuickDate('2026-05-19', UiDatePickerDirection.Past, TODAY)).toBe(QuickDate.Day)
    expect(matchQuickDate('2026-05-21', UiDatePickerDirection.Future, TODAY)).toBe(QuickDate.Day)
    expect(matchQuickDate('2026-05-21', UiDatePickerDirection.Past, TODAY)).toBe(QuickDate.Custom)
  })

  it('resolves range presets to their first and last day', () => {
    /**
     * Resolves a preset against the fixed day and renders both ends.
     *
     * @param preset - The preset under test.
     * @returns The ISO start and end.
     */
    const iso = (preset: DateRangePreset): string[] => presetRange(preset, TODAY).map(formatIsoDate)
    expect(iso(DateRangePreset.Today)).toEqual(['2026-05-20', '2026-05-20'])
    expect(iso(DateRangePreset.Last7Days)).toEqual(['2026-05-14', '2026-05-20'])
    expect(iso(DateRangePreset.ThisMonth)).toEqual(['2026-05-01', '2026-05-20'])
    expect(iso(DateRangePreset.LastMonth)).toEqual(['2026-04-01', '2026-04-30'])
    expect(iso(DateRangePreset.ThisYear)).toEqual(['2026-01-01', '2026-05-20'])
    expect(iso(DateRangePreset.LastYear)).toEqual(['2025-01-01', '2025-12-31'])
  })
})

describe('datePicker selected states', () => {
  it('fills the selected cell and active preset with a primary fill that carries the ground colour as text', () => {
    expect(dateCellSelectedClasses).toContain('bg-primary-600')
    expect(dateCellSelectedClasses).toContain('text-surface-base')
    expect(datePresetActiveClasses).toContain('bg-primary-600')
    expect(dateLibraryThemeClasses).toContain('[--dp-background-color:var(--surface-elevated)]')
  })
})
