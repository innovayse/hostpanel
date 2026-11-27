/**
 * Tests for `utils/datePicker.ts`: splitting and joining the ISO date, the days of a month and the year choices.
 *
 * @module tests/utils/datePicker.test
 */

import { describe, expect, it } from 'vitest'
import {
  DATE_PICKER_YEARS_AHEAD,
  DATE_PICKER_YEARS_BACK,
  MONTH_VALUES,
  daysInMonth,
  joinIsoDate,
  splitIsoDate,
  twoDigitValues,
  yearValues
} from '~/utils/datePicker'

describe('splitIsoDate', () => {
  it('splits a full ISO date and gives empty parts for anything else', () => {
    expect(splitIsoDate('2026-03-09')).toEqual({ year: '2026', month: '03', day: '09' })
    expect(splitIsoDate('09/03/2026')).toEqual({ year: '', month: '', day: '' })
    expect(splitIsoDate(null)).toEqual({ year: '', month: '', day: '' })
  })
})

describe('daysInMonth', () => {
  it('counts the days of a month, the leap day of a leap year included', () => {
    expect(daysInMonth('2026', '02')).toBe(28)
    expect(daysInMonth('2028', '02')).toBe(29)
    expect(daysInMonth('2026', '04')).toBe(30)
    expect(daysInMonth('2026', '12')).toBe(31)
  })

  it('offers 31 days until a month is chosen, and the leap day until a year is', () => {
    expect(daysInMonth('2026', '')).toBe(31)
    expect(daysInMonth('', '02')).toBe(29)
  })
})

describe('joinIsoDate', () => {
  it('joins three parts that name a real day', () => {
    expect(joinIsoDate({ year: '2026', month: '03', day: '09' })).toBe('2026-03-09')
  })

  it('is empty while a part is missing or the day does not exist in the month', () => {
    expect(joinIsoDate({ year: '2026', month: '03', day: '' })).toBe('')
    expect(joinIsoDate({ year: '2026', month: '02', day: '30' })).toBe('')
  })
})

describe('the choices', () => {
  it('lists two digit days and months and the years from the latest to the earliest', () => {
    expect(twoDigitValues(3)).toEqual(['01', '02', '03'])
    expect(MONTH_VALUES).toHaveLength(12)
    const years = yearValues(2026)
    expect(years[0]).toBe(String(2026 + DATE_PICKER_YEARS_AHEAD))
    expect(years.at(-1)).toBe(String(2026 - DATE_PICKER_YEARS_BACK))
  })
})
