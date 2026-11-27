/**
 * Date, time and text formatting helpers shared by the admin screens: localized dates, sizes, the
 * compact support date-time, time of day, elapsed and relative time, and day-string helpers.
 */
import { translateCountIn, translateIn } from '@/lib/translate'

/** What every screen shows where a value or a date is missing or unreadable: one em dash, defined once. */
export const EMPTY_VALUE = '—'

/** Separates the segments of one summary line, such as a status followed by its counts and version. */
export const PART_SEPARATOR = ' · '

/**
 * Joins the segments of a summary line, leaving out the ones that are missing so no separator dangles.
 *
 * @param parts - The segments in order; null, undefined and blank ones are skipped.
 * @returns The present segments joined with {@link PART_SEPARATOR}, empty when none is present.
 */
export const joinParts = (parts: ReadonlyArray<string | null | undefined>): string =>
  parts.filter((part): part is string => part !== null && part !== undefined && part.trim() !== '').join(PART_SEPARATOR)

/**
 * The letters of an avatar for a name: the first letters of its first two words, or the first two
 * letters of a single word.
 *
 * @param name - A person's name, or any label such as an email address.
 * @returns Up to two upper-case letters; empty for a blank name.
 */
export const initialsOf = (name: string): string => {
  const trimmed = name.trim()
  const [first = '', second = ''] = trimmed.split(/\s+/)
  const letters = second === '' ? trimmed.slice(0, 2) : `${first.charAt(0)}${second.charAt(0)}`
  return letters.toUpperCase()
}

/**
 * Formats an ISO date string as a localized date.
 *
 * @param iso - ISO 8601 date string, null, or undefined.
 * @param fallback - Text to return if the date is invalid. Defaults to {@link EMPTY_VALUE}.
 * @returns Formatted date string or the fallback.
 */
export const formatDate = (iso: string | null | undefined, fallback = EMPTY_VALUE): string => {
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
 * Tells whether this engine ships the date data of a locale. A browser can lack it (Armenian is the usual one) and then
 * silently writes month names in English, so callers word the month from the app's own messages instead.
 *
 * @param locale - Locale code to check.
 * @returns True when `Intl.DateTimeFormat` knows the locale.
 */
const hasDateLocaleData = (locale: string): boolean => Intl.DateTimeFormat.supportedLocalesOf([locale]).length > 0

/**
 * Writes the short name of a month in a language: from `Intl` where the engine has the locale's data, else from the
 * app's own `calendar.monthsShort` messages. The one place a short month name is made, so no caller falls back alone.
 *
 * @param month - Zero-based month, 0 for January.
 * @param locale - Language of the name, such as `hy`.
 * @returns The short month name, such as "Mar".
 */
export const shortMonthName = (month: number, locale: string): string =>
  hasDateLocaleData(locale)
    ? new Date(Date.UTC(2000, month, 1)).toLocaleDateString(locale, { month: 'short', timeZone: 'UTC' })
    : translateIn(`calendar.monthsShort.${month + 1}`, locale)

/**
 * Writes a calendar day as a short day and month in a language, in the order a reader of that language expects
 * (month first or day first), from `Intl` where the engine has the locale's data, else from the app's own month names.
 *
 * @param date - The calendar day, read in UTC.
 * @param locale - Language of the text.
 * @param dayFirst - Which order the app's own wording uses when the engine lacks the locale ("12 Mar" or "Mar 12").
 * @returns For example "Mar 7" or "12 Mar".
 */
export const formatShortDay = (date: Date, locale: string, dayFirst: boolean): string => {
  if (hasDateLocaleData(locale)) {
    return date.toLocaleDateString(locale, { month: 'short', day: 'numeric', timeZone: 'UTC' })
  }
  const month = shortMonthName(date.getUTCMonth(), locale)
  return dayFirst ? `${date.getUTCDate()} ${month}` : `${month} ${date.getUTCDate()}`
}

/**
 * Writes a moment as a medium date with the time of day in a language ("Mar 7, 2026, 9:30 AM"), from `Intl` where the
 * engine has the locale's data, else with the month from the app's own messages and a 24-hour time.
 *
 * @param moment - The moment, read in the viewer's time zone.
 * @param locale - Language of the text.
 * @returns The date and the time.
 */
export const formatMediumDateTime = (moment: Date, locale: string): string => {
  if (hasDateLocaleData(locale)) {
    return moment.toLocaleString(locale, { dateStyle: 'medium', timeStyle: 'short' })
  }
  return `${moment.getDate()} ${shortMonthName(moment.getMonth(), locale)} ${moment.getFullYear()}, ${pad(moment.getHours())}:${pad(moment.getMinutes())}`
}

/**
 * Formats a `YYYY-MM-DD` day as a short month and day in the given language, for a chart axis.
 *
 * The day is read as a calendar day (UTC), so the viewer's time zone never moves it.
 *
 * @param iso - Day in `YYYY-MM-DD` form.
 * @param locale - Language of the month name, such as `en`.
 * @returns The month and day, such as `Mar 7`; the input when it is not a day.
 */
export const formatMonthDay = (iso: string, locale: string): string => {
  const { year, month, day } = splitIsoDate(iso)
  if (isNaN(year) || isNaN(month) || isNaN(day)) {
    return iso
  }
  return formatShortDay(new Date(Date.UTC(year, month - 1, day)), locale, false)
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
 * @param fallback - Text to return if the timestamp is invalid. Defaults to {@link EMPTY_VALUE}.
 * @returns For example "14/10/2026, 09:30", or the fallback.
 */
export const formatDateTime = (iso: string | null | undefined, fallback = EMPTY_VALUE): string => {
  if (!iso) {
    return fallback
  }
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) {
    return fallback
  }
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

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
    return EMPTY_VALUE
  }
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return EMPTY_VALUE
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
    return EMPTY_VALUE
  }
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) {
    return EMPTY_VALUE
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

/** One step of the relative wording: the unit's `Intl` name, the message that words it without `Intl`, and its count. */
interface RelativeSpan {
  /** The unit `Intl.RelativeTimeFormat` counts in. */
  unit: Intl.RelativeTimeFormatUnit
  /** Catalogue key of the counted sentence in the app's own messages. */
  messageKey: string
  /** How many of the unit have passed. */
  count: number
}

/**
 * Picks the largest whole unit (minute, hour, day) that fits the seconds passed.
 *
 * @param seconds - Seconds since the moment, at least one minute.
 * @returns The unit, its message key and the count.
 */
const relativeSpan = (seconds: number): RelativeSpan => {
  const minutes = Math.floor(seconds / MINUTE_SECONDS)
  if (minutes < MINUTES_PER_HOUR) {
    return { unit: 'minute', messageKey: 'duration.minutesAgo', count: minutes }
  }
  const hours = Math.floor(minutes / MINUTES_PER_HOUR)
  if (hours < HOURS_PER_DAY) {
    return { unit: 'hour', messageKey: 'duration.hoursAgo', count: hours }
  }
  return { unit: 'day', messageKey: 'duration.daysAgo', count: Math.floor(hours / HOURS_PER_DAY) }
}

/**
 * Describes how long ago a moment was, in the given locale ("now", "5 minutes ago", "2 days ago").
 * Built on `Intl.RelativeTimeFormat`, so unit names and plurals come from the locale. A browser can ship without the
 * data of a locale (Armenian is the usual one) and then silently answers in English, so for such a locale the wording
 * comes from the app's own messages (`duration.*`, a plural form per count) instead.
 *
 * @param iso - ISO 8601 timestamp of the moment.
 * @param locale - Locale code the wording is produced in.
 * @param now - Current time in epoch milliseconds (injectable for tests).
 * @returns The relative wording.
 */
export const formatRelativeFromNow = (iso: string, locale: string, now: number = Date.now()): string => {
  const seconds = Math.max(0, Math.floor((now - new Date(iso).getTime()) / 1000))
  const formatter = Intl.RelativeTimeFormat.supportedLocalesOf([locale]).length > 0 ? new Intl.RelativeTimeFormat(locale, { numeric: 'auto' }) : null
  if (seconds < MINUTE_SECONDS) {
    return formatter === null ? translateIn('duration.relativeNow', locale) : formatter.format(0, 'second')
  }
  const { unit, messageKey, count } = relativeSpan(seconds)
  return formatter === null ? translateCountIn(messageKey, count, locale) : formatter.format(-count, unit)
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

/**
 * Writes a decimal number with a fixed count of fraction digits in the locale's own separators.
 *
 * @param value - The number.
 * @param locale - Locale code the number is written in.
 * @param fractionDigits - Digits kept after the separator; one when omitted.
 * @returns The number text, such as "2.5" or "2,5".
 */
export const formatDecimal = (value: number, locale: string, fractionDigits = 1): string =>
  new Intl.NumberFormat(locale, { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(value)

/**
 * Writes a count in the short form of the locale ("27" stays "27", "1234" becomes "1.2K"), for a place too narrow for the
 * full digits such as a card footer. Built on `Intl.NumberFormat`, so the abbreviation and separators come from the locale.
 *
 * @param value - The count.
 * @param locale - Locale code the number is written in.
 * @returns The short count text.
 */
export const formatCompactNumber = (value: number, locale: string): string =>
  new Intl.NumberFormat(locale, { notation: 'compact', maximumFractionDigits: 1 }).format(value)

/** Units in the next unit up (a gigabyte is 1024 megabytes), for {@link formatSize}. */
const SIZE_STEP = 1024

/** Unit names of a size given in megabytes, smallest first, for {@link formatSizeMb}. */
const SIZE_UNITS_FROM_MB = ['MB', 'GB', 'TB'] as const

/** Unit names of a size given in bytes, smallest first, for {@link formatSizeBytes}. */
const SIZE_UNITS_FROM_BYTES = ['B', 'KB', 'MB', 'GB'] as const

/**
 * Writes a size in the largest of the given units that keeps it at or above 1, with at most one decimal.
 *
 * @param amount - The size in the first of the units.
 * @param units - Unit names, smallest first; each is {@link SIZE_STEP} times the one before it.
 * @param locale - Locale code the number is written in.
 * @returns The size with its unit.
 */
const formatSize = (amount: number, units: readonly string[], locale: string): string => {
  let value = amount
  let unit = 0
  while (value >= SIZE_STEP && unit < units.length - 1) {
    value /= SIZE_STEP
    unit++
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value)} ${units[unit]}`
}

/**
 * Writes a size given in megabytes in the largest unit that keeps it at or above 1, with at most
 * one decimal ("512" becomes "512 MB", "1536" becomes "1.5 GB", "2048" becomes "2 GB").
 *
 * @param megabytes - The size in megabytes; the API's disk and bandwidth limits use this unit.
 * @param locale - Locale code the number is written in.
 * @returns The size with its unit.
 */
export const formatSizeMb = (megabytes: number, locale: string): string => formatSize(megabytes, SIZE_UNITS_FROM_MB, locale)

/**
 * Writes a size given in bytes in the largest unit that keeps it at or above 1, with at most one
 * decimal ("512" becomes "512 B", "1536" becomes "1.5 KB", "5242880" becomes "5 MB").
 *
 * @param bytes - The size in bytes; a file's length is in this unit.
 * @param locale - Locale code the number is written in.
 * @returns The size with its unit.
 */
export const formatSizeBytes = (bytes: number, locale: string): string => formatSize(bytes, SIZE_UNITS_FROM_BYTES, locale)
