/**
 * Date, time and text formatting helpers shared by the admin screens: localized dates, sizes, the
 * compact support date-time, time of day, elapsed and relative time, and day-string helpers.
 */

/**
 * Formats an ISO date string as a localized date.
 *
 * @param iso - ISO 8601 date string, null, or undefined.
 * @param fallback - Text to return if the date is invalid. Defaults to '\u2014'.
 * @returns Formatted date string or the fallback.
 */
export const formatDate = (iso: string | null | undefined, fallback = '\u2014'): string => {
  if (!iso) {
    return fallback
  }
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) {
    return fallback
  }
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/**
 * Extracts the YYYY-MM-DD portion from an ISO date string for input[type=date].
 *
 * @param iso - ISO 8601 date string or undefined.
 * @returns Date string suitable for date input, or empty string.
 */
export const toDateInputValue = (iso: string | null | undefined): string => {
  if (!iso) {
    return ''
  }
  const d = new Date(iso)
  if (isNaN(d.getTime())) {
    return ''
  }
  return d.toISOString().split('T')[0] ?? ''
}

/**
 * Splits a `YYYY-MM-DD` date string into its numeric year, month and day parts.
 *
 * Exists because `noUncheckedIndexedAccess` types the result of `String.split` as
 * `(string | undefined)[]`, which every caller would otherwise have to widen by hand.
 * A missing part yields `NaN`. Callers here always pass a string that has already been
 * validated as a full date, so that branch is unreachable in practice.
 *
 * @param iso - Date string in `YYYY-MM-DD` form.
 * @returns The three parts as numbers; `NaN` for any part the string did not contain.
 */
export const splitIsoDate = (iso: string): { year: number; month: number; day: number } => {
  const [year = '', month = '', day = ''] = iso.split('-')
  return { year: parseInt(year), month: parseInt(month), day: parseInt(day) }
}

/**
 * Renders a `Date` as its UTC `YYYY-MM-DD` day, the form every date `<input>` and every
 * date-keyed lookup in the admin uses.
 *
 * `Date.toISOString()` always contains a `T`, so the first split part is always present,
 * but `noUncheckedIndexedAccess` cannot know that, and spelling the fallback out at every
 * call site would bury the intent. Callers must pass a valid `Date`; an invalid one makes
 * `toISOString()` throw.
 *
 * @param date - The date to render. Must be a valid `Date`.
 * @returns The UTC calendar day as `YYYY-MM-DD`.
 */
export const toIsoDay = (date: Date): string => date.toISOString().split('T')[0] ?? ''

/**
 * Formats an ISO timestamp as a localized date with the time of day.
 *
 * @param iso - ISO 8601 timestamp, null, or undefined.
 * @param fallback - Text to return if the timestamp is invalid. Defaults to an em dash.
 * @returns For example "14/10/2026, 09:30", or the fallback.
 */
export const formatDateTime = (iso: string | null | undefined, fallback = '—'): string => {
  if (!iso) {
    return fallback
  }
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) {
    return fallback
  }
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

/** What is shown for a missing or unreadable date in the support formatters. */
const NO_DATE = '—'

/** Seconds in a day. */
const DAY_SECONDS = 86400

/** Seconds in an hour. */
const HOUR_SECONDS = 3600

/** Seconds in a minute. */
const MINUTE_SECONDS = 60

/** Minutes in an hour. */
const MINUTES_PER_HOUR = 60

/** Hours in a day. */
const HOURS_PER_DAY = 24

/**
 * Pads a number to two digits.
 *
 * @param value - The number to pad.
 * @returns The number as at least two characters.
 */
const pad = (value: number): string => String(value).padStart(2, '0')

/**
 * Formats an ISO date string as DD/MM/YYYY HH:MM in the viewer's time zone (the support pages'
 * date-time; unlike {@link formatDateTime} it has no comma and accepts any year).
 *
 * @param iso - ISO 8601 date string, null or undefined.
 * @returns The formatted date-time, or a dash when there is none or it is unreadable.
 */
export const formatDateTimeCompact = (iso: string | null | undefined): string => {
  if (!iso) {
    return NO_DATE
  }
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return NO_DATE
  }
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/**
 * Formats the time of day (with seconds) of an ISO timestamp in a locale.
 *
 * @param iso - ISO 8601 timestamp.
 * @param locale - Locale code the time is written in.
 * @returns The local time, or an empty string when the input cannot be parsed.
 */
export const formatClock = (iso: string, locale: string): string => {
  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? ''
    : date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

/**
 * Formats the time of day, without seconds, of a moment such as when a chat message was written.
 *
 * @param moment - The moment, in milliseconds since the epoch.
 * @param locale - Locale code the time is written in.
 * @returns The local time, or an empty string when the moment cannot be read.
 */
export const formatTime = (moment: number | undefined, locale: string): string => {
  if (moment === undefined) {
    return ''
  }
  const date = new Date(moment)
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })
}

/**
 * Formats the time passed since a moment as `6d 1h 17m` (zero units dropped, minutes always kept).
 *
 * @param iso - ISO 8601 timestamp, null or undefined.
 * @param now - The current time in milliseconds; defaults to the clock, injectable for tests.
 * @returns The elapsed time, or a dash when there is no timestamp or it is unreadable.
 */
export const formatElapsed = (iso: string | null | undefined, now: number = Date.now()): string => {
  if (!iso) {
    return NO_DATE
  }
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) {
    return NO_DATE
  }
  let remaining = Math.max(0, Math.floor((now - then) / 1000))
  const days = Math.floor(remaining / DAY_SECONDS)
  remaining %= DAY_SECONDS
  const hours = Math.floor(remaining / HOUR_SECONDS)
  remaining %= HOUR_SECONDS
  const minutes = Math.floor(remaining / MINUTE_SECONDS)
  const parts: string[] = []
  if (days > 0) {
    parts.push(`${days}d`)
  }
  if (hours > 0) {
    parts.push(`${hours}h`)
  }
  parts.push(`${minutes}m`)
  return parts.join(' ')
}

/**
 * Describes how long ago a moment was, in the given locale ("now", "5 minutes ago", "2 days ago").
 * Built on `Intl.RelativeTimeFormat`, so unit names and plurals come from the locale instead of a
 * sentence written here.
 *
 * @param iso - ISO 8601 timestamp of the moment.
 * @param locale - Locale code the wording is produced in.
 * @param now - Current time in epoch milliseconds (injectable for tests).
 * @returns The relative wording.
 */
export const formatRelativeFromNow = (iso: string, locale: string, now: number = Date.now()): string => {
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  const seconds = Math.max(0, Math.floor((now - new Date(iso).getTime()) / 1000))
  if (seconds < MINUTE_SECONDS) {
    return formatter.format(0, 'second')
  }
  const minutes = Math.floor(seconds / MINUTE_SECONDS)
  if (minutes < MINUTES_PER_HOUR) {
    return formatter.format(-minutes, 'minute')
  }
  const hours = Math.floor(minutes / MINUTES_PER_HOUR)
  if (hours < HOURS_PER_DAY) {
    return formatter.format(-hours, 'hour')
  }
  return formatter.format(-Math.floor(hours / HOURS_PER_DAY), 'day')
}

/**
 * Formats a share of a whole as a percentage in the given locale, with at most one decimal
 * ("0.5" becomes "50%", "0.1234" becomes "12.3%"). Built on `Intl.NumberFormat`, so the sign
 * placement and digits come from the locale.
 *
 * @param share - The share, from 0 to 1.
 * @param locale - Locale code the number is written in.
 * @returns The percentage text.
 */
export const formatShare = (share: number, locale: string): string =>
  new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1 }).format(share)

/** Megabytes in a gigabyte and gigabytes in a terabyte, for {@link formatSizeMb}. */
const SIZE_STEP = 1024

/** Unit names from megabytes upward, for {@link formatSizeMb}. */
const SIZE_UNITS = ['MB', 'GB', 'TB'] as const

/**
 * Writes a size given in megabytes in the largest unit that keeps it at or above 1, with at most
 * one decimal ("512" becomes "512 MB", "1536" becomes "1.5 GB", "2048" becomes "2 GB").
 *
 * @param megabytes - The size in megabytes; the API's disk and bandwidth limits use this unit.
 * @param locale - Locale code the number is written in.
 * @returns The size with its unit.
 */
export const formatSizeMb = (megabytes: number, locale: string): string => {
  let value = megabytes
  let unit = 0
  while (value >= SIZE_STEP && unit < SIZE_UNITS.length - 1) {
    value /= SIZE_STEP
    unit++
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value)} ${SIZE_UNITS[unit]}`
}
