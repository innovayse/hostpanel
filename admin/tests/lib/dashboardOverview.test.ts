/**
 * Tests for the dashboard helpers: the part of the day, the signed change text, the chart day caption and the
 * server load share.
 */
import { describe, expect, it } from 'vitest'
import { dayPartOf, DayPart, formatChange, formatDayCaption, serverLoadPercent } from '@/lib/dashboardOverview'
import type { ServerDto } from '@/types/server'

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

describe('formatChange', () => {
  it('writes the sign so it reads without colour', () => {
    expect(formatChange(12.4)).toBe('+12%')
    expect(formatChange(-4.6)).toBe('-5%')
    expect(formatChange(0)).toBe('0%')
  })
})

describe('formatDayCaption', () => {
  it('writes the day and the short month in the language given', () => {
    expect(formatDayCaption('2026-03-12', 'en')).toBe('Mar 12')
    expect(formatDayCaption('2026-03-12', 'ru')).toMatch(/^12 мар/)
  })

  it('does not move the day with the time zone of the machine', () => {
    expect(formatDayCaption('2026-01-01', 'en')).toBe('Jan 1')
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
