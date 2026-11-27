/**
 * Pure helpers of the kit date picker: the day, month and year choices and the ISO `yyyy-MM-dd` text they make.
 *
 * @module utils/datePicker
 */

/** How many years before this one the year list starts. */
export const DATE_PICKER_YEARS_BACK = 100

/** How many years after this one the year list ends. */
export const DATE_PICKER_YEARS_AHEAD = 20

/** The days of one month and the months of a year, as the numbers the lists offer. */
const MONTHS_IN_YEAR = 12

/** The parts of a date the three lists hold; empty text for a part that is not chosen yet. */
export interface DateParts {
  /** The year, four digits, or empty. */
  year: string
  /** The month, two digits, or empty. */
  month: string
  /** The day of the month, two digits, or empty. */
  day: string
}

/**
 * Splits an ISO date into the parts the lists show.
 *
 * @param iso - `yyyy-MM-dd`, or anything else for none.
 * @returns The parts; every part empty when the text is not an ISO date.
 */
export const splitIsoDate = (iso: string | null | undefined): DateParts => {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '')
  const [, year = '', month = '', day = ''] = match ?? []
  return { year, month, day }
}

/**
 * Tells how many days a month has.
 *
 * @param year - The year, four digits; empty counts as a leap year so February offers its 29th until a year is chosen.
 * @param month - The month, two digits.
 * @returns The number of days, 31 when the month is not chosen.
 */
export const daysInMonth = (year: string, month: string): number => {
  const monthNumber = Number(month)
  if (!monthNumber) {
    return 31
  }
  // Day 0 of the next month is the last day of this one; a missing year uses a leap year.
  return new Date(Number(year) || 2000, monthNumber, 0).getDate()
}

/**
 * Joins the parts into an ISO date.
 *
 * @param parts - The parts as chosen.
 * @returns `yyyy-MM-dd` when the three parts name a real day, else an empty text (a day past the end of the month is dropped).
 */
export const joinIsoDate = (parts: DateParts): string => {
  if (!parts.year || !parts.month || !parts.day || Number(parts.day) > daysInMonth(parts.year, parts.month)) {
    return ''
  }
  return `${parts.year}-${parts.month}-${parts.day}`
}

/**
 * Builds the two digit choices 1 to `count`.
 *
 * @param count - How many numbers.
 * @returns The values, each padded to two digits.
 */
export const twoDigitValues = (count: number): string[] =>
  Array.from({ length: count }, (_, index) => String(index + 1).padStart(2, '0'))

/**
 * Builds the year choices, the latest first.
 *
 * @param currentYear - This year.
 * @returns The years from `currentYear + DATE_PICKER_YEARS_AHEAD` down to `currentYear - DATE_PICKER_YEARS_BACK`, as text.
 */
export const yearValues = (currentYear: number): string[] =>
  Array.from(
    { length: DATE_PICKER_YEARS_BACK + DATE_PICKER_YEARS_AHEAD + 1 },
    (_, index) => String(currentYear + DATE_PICKER_YEARS_AHEAD - index)
  )

/** The month numbers the month list offers, two digits each. */
export const MONTH_VALUES: string[] = twoDigitValues(MONTHS_IN_YEAR)
