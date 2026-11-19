/**
 * Helpers of the time zone setting, one file for the area: the one source of the IANA zones the pickers offer, the
 * labels that show a zone's city and current offset, the region groups, and the reading of the legacy `UTC+4` spelling
 * the setting used to hold as free text.
 *
 * The setting stores an IANA id (`Asia/Yerevan`); everything the operator reads (city, offset, the zone's own name in
 * the interface language) is derived here from the browser's `Intl` data, so no zone list is written in the source.
 */
import { translate } from '@/lib/translate'
import type { UiSelectOption } from '@/utils/ui/select'

/** The id of coordinated universal time, the one zone `Intl.supportedValuesOf` leaves out in some engines. */
export const UTC_ZONE_ID = 'UTC'

/** The prefix of the fixed-offset zones (`Etc/GMT-4`), which carry no city. */
const ETC_PREFIX = 'Etc/'

/** The legacy free-text spelling of an offset: `UTC+4`, `GMT-3`, `utc + 05`. */
const LEGACY_OFFSET_PATTERN = /^(?:UTC|GMT)\s*([+-])\s*(\d{1,2})$/i

/** The largest whole-hour offset west of UTC a fixed `Etc/GMT` zone exists for. */
const ETC_WEST_MAX_HOURS = 12

/** The largest whole-hour offset east of UTC a fixed `Etc/GMT` zone exists for. */
const ETC_EAST_MAX_HOURS = 14

/** The options of one language together with the hour their offsets were read at. */
interface CachedOptions {
  /** The hour (`2026-01-15T12`) the offsets were taken at; daylight saving time moves them, so a later hour rebuilds. */
  hour: string
  /** The built options. */
  options: readonly UiSelectOption<string>[]
}

/** Labels per language, rebuilt when the hour changes: formatting four hundred zones twice is too much work to repeat on every render. */
const optionCache = new Map<string, CachedOptions>()

/** How many characters of an ISO timestamp name the hour (`2026-01-15T12`). */
const ISO_HOUR_LENGTH = 13

/**
 * Reads one part of a zone's formatted date.
 *
 * @param zone - IANA zone id.
 * @param locale - BCP 47 language tag the part is written in.
 * @param style - Which time zone name style to read.
 * @param at - The moment the offset is taken at.
 * @returns The part's text, or an empty string when the engine gives none.
 */
const zoneNamePart = (zone: string, locale: string, style: Intl.DateTimeFormatOptions['timeZoneName'], at: Date): string =>
  new Intl.DateTimeFormat(locale, { timeZone: zone, timeZoneName: style }).formatToParts(at).find((part) => part.type === 'timeZoneName')?.value ?? ''

/**
 * The current offset of a zone from UTC.
 *
 * @param zone - IANA zone id.
 * @param at - The moment the offset is taken at; daylight saving time moves it through the year.
 * @returns The offset as `UTC+04:00`; `UTC+00:00` for zero.
 */
export const utcOffsetLabel = (zone: string, at: Date = new Date()): string => {
  const offset = zoneNamePart(zone, 'en-US', 'longOffset', at)
  return offset === 'GMT' || offset === '' ? 'UTC+00:00' : offset.replace(/^GMT/, 'UTC')
}

/**
 * The city of a zone, read from the last segment of its id.
 *
 * @param zone - IANA zone id such as `America/Argentina/Buenos_Aires`.
 * @returns `Buenos Aires`; the whole id for a zone without a city (`UTC`, `Etc/GMT-4`).
 */
const cityOf = (zone: string): string => (zone.includes('/') && !zone.startsWith(ETC_PREFIX) ? (zone.split('/').pop() ?? zone).replaceAll('_', ' ') : zone)

/**
 * The label of a zone: its city and its current offset.
 *
 * @param zone - IANA zone id.
 * @param at - The moment the offset is taken at.
 * @returns `Yerevan (UTC+04:00)`; just `UTC+04:00` for a fixed-offset zone, which has no city.
 */
export const timezoneLabel = (zone: string, at: Date = new Date()): string => {
  const offset = utcOffsetLabel(zone, at)
  return zone === UTC_ZONE_ID || zone.startsWith(ETC_PREFIX) ? offset : `${cityOf(zone)} (${offset})`
}

/**
 * Whether the engine knows a zone id.
 *
 * @param zone - Text to test.
 * @returns True when `Intl` accepts the id as a time zone.
 */
export const isKnownTimezone = (zone: string): boolean => {
  if (zone.trim() === '') {
    return false
  }
  try {
    new Intl.DateTimeFormat('en-US', { timeZone: zone })
    return true
  } catch {
    return false
  }
}

/**
 * Reads the legacy `UTC+4` spelling as the fixed-offset IANA zone that means the same.
 *
 * @param value - What the setting holds.
 * @returns `Etc/GMT-4` for `UTC+4` (the Etc zones count the offset the POSIX way round); `UTC` for a zero offset; null when the value is
 *   not an offset in whole hours the Etc zones cover.
 */
export const legacyOffsetZone = (value: string): string | null => {
  const match = LEGACY_OFFSET_PATTERN.exec(value.trim())
  if (match === null) {
    return null
  }
  const hours = Number(match[2])
  if (hours === 0) {
    return UTC_ZONE_ID
  }
  const east = match[1] === '+'
  if (hours > (east ? ETC_EAST_MAX_HOURS : ETC_WEST_MAX_HOURS)) {
    return null
  }
  return `${ETC_PREFIX}GMT${east ? '-' : '+'}${hours}`
}

/**
 * The zone id a stored value stands for: an IANA id as it is, a legacy offset as its fixed-offset zone.
 *
 * @param value - What the setting holds.
 * @returns The IANA id; the value unchanged when it is neither (the picker then shows it as it is).
 */
export const timezoneIdOf = (value: string): string => legacyOffsetZone(value) ?? value.trim()

/**
 * The region group a zone is listed under.
 *
 * @param zone - IANA zone id.
 * @returns The first segment of the id (`Asia`); `UTC` for the universal zone.
 */
const regionOf = (zone: string): string => (zone.includes('/') ? (zone.split('/')[0] ?? zone) : zone)

/**
 * The offset of a zone in minutes, for ordering.
 *
 * @param zone - IANA zone id.
 * @param at - The moment the offset is taken at.
 * @returns Minutes east of UTC.
 */
const offsetMinutesOf = (zone: string, at: Date): number => {
  const match = /^UTC([+-])(\d{2}):(\d{2})$/.exec(utcOffsetLabel(zone, at))
  if (match === null) {
    return 0
  }
  return (match[1] === '-' ? -1 : 1) * (Number(match[2]) * 60 + Number(match[3]))
}

/**
 * The picker's options: every IANA zone the engine knows, grouped by region and ordered by offset then city.
 *
 * The row's label is the city and the current offset; the row's second line is the zone's own name in the interface
 * language ("Armenia Time"), and the id itself is searchable.
 *
 * @param locale - BCP 47 language tag of the interface.
 * @param at - The moment the offsets are taken at.
 * @returns The options, with the region name (translated) as each row's group.
 */
export const timezoneOptions = (locale: string, at: Date = new Date()): readonly UiSelectOption<string>[] => {
  const hour = at.toISOString().slice(0, ISO_HOUR_LENGTH)
  const cached = optionCache.get(locale)
  if (cached !== undefined && cached.hour === hour) {
    return cached.options
  }
  const zones = [...new Set([UTC_ZONE_ID, ...Intl.supportedValuesOf('timeZone')])]
  const rows = zones.map((zone) => ({
    zone,
    region: regionOf(zone),
    offsetMinutes: offsetMinutesOf(zone, at),
    option: {
      value: zone,
      label: timezoneLabel(zone, at),
      description: zoneNamePart(zone, locale, 'longGeneric', at) || undefined,
      keywords: zone,
      group: translate(`settings.timezone.regions.${regionOf(zone)}`),
    } satisfies UiSelectOption<string>,
  }))
  rows.sort((a, b) => a.region.localeCompare(b.region) || a.offsetMinutes - b.offsetMinutes || a.option.label.localeCompare(b.option.label))
  const options = rows.map((row) => row.option)
  optionCache.set(locale, { hour, options })
  return options
}
