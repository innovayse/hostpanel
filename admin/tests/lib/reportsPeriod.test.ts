/**
 * Tests for the month and year choices of the report filters.
 */
import { describe, expect, it } from 'vitest'
import { currentMonthNumber, currentYear, monthName, monthOptions, yearOptions } from '@/lib/reportsPeriod'

describe('monthOptions', () => {
  it('lists the twelve months, January first, with their names', () => {
    const months = monthOptions()

    expect(months).toHaveLength(12)
    expect(months[0]).toEqual({ value: 1, label: 'January' })
    expect(months[11]?.value).toBe(12)
  })
})

describe('yearOptions', () => {
  it('lists years from the newest down, around the current one', () => {
    const current = new Date().getFullYear()
    const years = yearOptions(2, 1)

    expect(years.map(year => year.value)).toEqual([current + 1, current, current - 1, current - 2])
    expect(years[1]?.label).toBe(String(current))
  })

  it('offers only the current year when nothing is asked around it', () => {
    expect(yearOptions(0)).toHaveLength(1)
  })
})

describe('monthName', () => {
  it('names a month by number', () => {
    expect(monthName(3)).toBe('March')
  })
})

describe('current period defaults', () => {
  it('reads the current year and month number from the clock', () => {
    const now = new Date()

    expect(currentYear()).toBe(now.getFullYear())
    expect(currentMonthNumber()).toBe(now.getMonth() + 1)
  })
})
