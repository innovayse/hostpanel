/**
 * Pure helpers behind the dashboard panels: the greeting's part of the day, the signed change text, the
 * caption of a chart day and the load share of a server.
 */

import type { ServerDto } from '@/types/server'

/** The full bar, in percent. */
const FULL_PERCENT = 100

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
 * Short caption of a chart day, in the language of the screen.
 *
 * @param date - The day as `yyyy-MM-dd`.
 * @param locale - BCP 47 locale tag of the screen.
 * @returns For example "12 Mar".
 */
export const formatDayCaption = (date: string, locale: string): string =>
  new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`))

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
