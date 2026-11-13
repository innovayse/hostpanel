/**
 * Tests for the dashboard helpers: the month-over-month change, the target average, the signed
 * change text and the server load share.
 */
import { describe, expect, it } from 'vitest'
import { averageIncome, dayPartOf, DashboardPeriod, DayPart, formatChange, incomeInPeriod, monthlyIncomeChange, serverLoadPercent } from '@/lib/dashboardOverview'
import type { ReportMonthIncome } from '@/types/reportmonthincome'
import type { ServerDto } from '@/types/server'

/**
 * Builds income rows from plain amounts.
 *
 * @param amounts - Income per month, January first.
 * @returns The rows.
 */
const income = (...amounts: number[]): ReportMonthIncome[] =>
  amounts.map((amount, index) => ({ month: `M${index + 1}`, amount, currency: 'USD' }))

/**
 * Builds a server with only the fields the load share reads.
 *
 * @param accountsCount - Accounts from the last connection check.
 * @param maxAccounts - Capacity limit.
 * @returns The server.
 */
const server = (accountsCount: number | null, maxAccounts: number | null): ServerDto =>
  ({ accountsCount, limits: { maxAccounts, isDefault: false, isDisabled: false } }) as ServerDto

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

describe('incomeInPeriod', () => {
  it('takes the current month and the one before for 30 days', () => {
    expect(incomeInPeriod(income(1, 2, 3, 4, 5), 4, DashboardPeriod.Days30).map((m) => m.amount)).toEqual([4, 5])
  })

  it('takes the current month and the three before for 90 days', () => {
    expect(incomeInPeriod(income(1, 2, 3, 4, 5), 4, DashboardPeriod.Days90).map((m) => m.amount)).toEqual([2, 3, 4, 5])
  })

  it('stops at January', () => {
    expect(incomeInPeriod(income(1, 2), 1, DashboardPeriod.Days90).map((m) => m.amount)).toEqual([1, 2])
  })
})

describe('monthlyIncomeChange', () => {
  it('compares the month with the one before', () => {
    expect(monthlyIncomeChange(income(100, 150), 1)).toBe(50)
    expect(monthlyIncomeChange(income(200, 100), 1)).toBe(-50)
  })

  it('has no change in January or after an empty month', () => {
    expect(monthlyIncomeChange(income(100), 0)).toBeNull()
    expect(monthlyIncomeChange(income(0, 100), 1)).toBeNull()
  })
})

describe('averageIncome', () => {
  it('averages the months up to the current one', () => {
    expect(averageIncome(income(100, 200, 900), 1)).toBe(150)
  })

  it('is undefined without data', () => {
    expect(averageIncome([], 3)).toBeUndefined()
  })
})

describe('formatChange', () => {
  it('writes the sign so it reads without colour', () => {
    expect(formatChange(12.4)).toBe('+12%')
    expect(formatChange(-4.6)).toBe('-5%')
    expect(formatChange(0)).toBe('0%')
  })
})

describe('serverLoadPercent', () => {
  it('is the accounts over the capacity', () => {
    expect(serverLoadPercent(server(30, 120))).toBe(25)
  })

  it('is null for unlimited capacity or a server never checked', () => {
    expect(serverLoadPercent(server(30, null))).toBeNull()
    expect(serverLoadPercent(server(null, 100))).toBeNull()
  })
})
