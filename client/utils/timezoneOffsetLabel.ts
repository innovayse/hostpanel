/**
 * Rendering the time zone the operator published beside the working hours.
 *
 * The setting holds an IANA id (`Asia/Yerevan`), which a visitor cannot read at a glance, so the page shows the zone's
 * current offset (`UTC+04:00`). A row written before the setting held an id still holds free text (`UTC+4`): it is
 * shown as the operator wrote it.
 *
 * @module utils/timezoneOffsetLabel
 */

/**
 * The current offset of an IANA zone, as the page prints it.
 *
 * @param value - What the `portal.contact.timezone` setting holds.
 * @param at - The moment the offset is taken at; daylight saving time moves it through the year.
 * @returns `UTC+04:00` for an IANA id the platform knows (`UTC+00:00` for a zero offset); the value unchanged for any other text.
 */
export const timezoneOffsetLabel = (value: string, at: Date = new Date()): string => {
  try {
    const offset = new Intl.DateTimeFormat('en-US', { timeZone: value, timeZoneName: 'longOffset' })
      .formatToParts(at)
      .find((part) => part.type === 'timeZoneName')?.value
    if (offset === undefined) {
      return value
    }
    return offset === 'GMT' ? 'UTC+00:00' : offset.replace(/^GMT/, 'UTC')
  } catch {
    return value
  }
}
