/**
 * Pure helpers behind the dashboard panels: the month-over-month change of income, the months the
 * period toggle shows, the reference line of the revenue chart and the load share of a server.
 */

import type { ReportMonthIncome } from '@/types/reportmonthincome'
import type { ServerDto } from '@/types/server'

/** The full bar, in percent. */
const FULL_PERCENT = 100

/** Days one calendar month stands for when a period is turned into whole months. */
const DAYS_PER_MONTH = 30

/** Closed set of periods the dashboard toggle offers; the value is also the toggle option value. */
export const DashboardPeriod = {
  /** The last 30 days. */
  Days30: '30',
  /** The last 90 days. */
  Days90: '90',
} as const

/** One of the dashboard periods. */
export type DashboardPeriod = (typeof DashboardPeriod)[keyof typeof DashboardPeriod]

/** Closed set of parts of the day the dashboard greets for; the value is also the locale key suffix. */
export const DayPart = {
  /** Before noon. */
  Morning: 'morning',
  /** From noon until six in the evening. */
  Afternoon: 'afternoon',
  /** From six in the evening. */
  Evening: 'evening',
} as const

/** One of the parts of the day. */
export type DayPart = (typeof DayPart)[keyof typeof DayPart]

/** Hour from which the afternoon starts. */
const NOON_HOUR = 12

/** Hour from which the evening starts. */
const EVENING_HOUR = 18

/**
 * The part of the day an hour falls in.
 *
 * @param hour - Hour of the day, 0 to 23.
 * @returns Morning before noon, afternoon until six in the evening, evening after.
 */
export const dayPartOf = (hour: number): DayPart => {
  if (hour < NOON_HOUR) {
    return DayPart.Morning
  }
  return hour < EVENING_HOUR ? DayPart.Afternoon : DayPart.Evening
}

/**
 * The months of the year that overlap the last days of a period, ending with the current month.
 *
 * @param income - Income per calendar month of the year, January first.
 * @param monthIndex - Zero-based index of the current month.
 * @param period - The period to cover, a {@link DashboardPeriod} value (the toggle hands it over as plain text).
 * @returns The current month and the months before it that the period reaches into (30 days: two, 90 days: four).
 */
export const incomeInPeriod = (income: ReportMonthIncome[], monthIndex: number, period: string): ReportMonthIncome[] => {
  const months = Number(period) / DAYS_PER_MONTH + 1
  return income.slice(Math.max(0, monthIndex + 1 - months), monthIndex + 1)
}

/**
 * Change of income from the previous month to the current one.
 *
 * @param income - Income per calendar month of the year, January first.
 * @param monthIndex - Zero-based index of the current month.
 * @returns The change in percent, or null when there is no previous month or it earned nothing.
 */
export const monthlyIncomeChange = (income: ReportMonthIncome[], monthIndex: number): number | null => {
  const current = income[monthIndex]
  const previous = income[monthIndex - 1]
  if (!current || !previous || previous.amount <= 0) {
    return null
  }
  return ((current.amount - previous.amount) / previous.amount) * FULL_PERCENT
}

/**
 * Average monthly income so far this year, the reference line of the revenue chart.
 *
 * @param income - Income per calendar month of the year, January first.
 * @param monthIndex - Zero-based index of the current month.
 * @returns The average over the months up to and including the current one, or undefined without data.
 */
export const averageIncome = (income: ReportMonthIncome[], monthIndex: number): number | undefined => {
  const elapsed = income.slice(0, monthIndex + 1)
  if (elapsed.length === 0) {
    return undefined
  }
  return elapsed.reduce((sum, month) => sum + month.amount, 0) / elapsed.length
}

/**
 * Formats a change with its sign, so it reads without colour.
 *
 * @param change - The change in percent.
 * @returns For example "+12%" or "-4%".
 */
export const formatChange = (change: number): string => {
  const rounded = Math.round(change)
  return `${rounded > 0 ? '+' : ''}${rounded}%`
}

/**
 * Share of a server's account capacity in use.
 *
 * @param server - The server.
 * @returns The share in percent, or null for a server with unlimited capacity or no test result yet.
 */
export const serverLoadPercent = (server: ServerDto): number | null => {
  const { maxAccounts } = server.limits
  if (server.accountsCount === null || maxAccounts === null || maxAccounts <= 0) {
    return null
  }
  return (server.accountsCount / maxAccounts) * FULL_PERCENT
}
