/**
 * Rendering the date a testimonial was given, as a month and a year in the reader's language.
 *
 * @module utils/testimonialDate
 */

/**
 * Formats a testimonial date as a long month and a year, e.g. "March 2026" or "март 2026 г.".
 *
 * The month is read in UTC: the API sends an instant and the testimonial means "that month",
 * so converting it into the reader's zone could only move a date near a month boundary.
 *
 * @param value The ISO date the API sent; null, empty or unparseable values are tolerated.
 * @param locale The active i18n locale code (`en`, `ru`, `hy`, `de`, `es`, `fr`).
 * @returns The localised month and year, or an empty string when there is no date to show.
 */
export const formatTestimonialDate = (value: string | null | undefined, locale: string): string => {
  if (!value) {
    return ''
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  try {
    return new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date)
  }
  catch {
    // An unknown locale tag makes Intl throw a RangeError; the English month is better than none.
    return new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date)
  }
}
