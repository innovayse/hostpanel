/**
 * The quarter options every report that asks for a quarter offers, built in one place.
 *
 * Quarters of the last few calendar years, newest first, grouped by year. A row in the list reads
 * `Q4 · Oct–Dec` under its year heading; once picked, the trigger reads `Q4 2026 · Oct–Dec` on one
 * line. The month names are short and come from `Intl` in the reader's language.
 */
import type { UiSelectOption } from '@/utils/ui/select'

/** Number of quarters in a year. */
const QUARTERS_PER_YEAR = 4

/** Number of months in a quarter. */
const MONTHS_PER_QUARTER = 3

/** Prefix of a quarter's number. */
const QUARTER_PREFIX = 'Q'

/** Separates the quarter from its months. */
const PART_SEPARATOR = ' · '

/** Joins the first and the last month of a quarter. */
const MONTH_RANGE_SEPARATOR = '–'

/**
 * Builds the value of a quarter option, the form the reports send to the API.
 *
 * @param year - Calendar year.
 * @param quarter - Quarter number, 1 to 4.
 * @returns `<year>-<quarter>`.
 */
export const quarterValue = (year: number, quarter: number): string => `${year}-${quarter}`

/**
 * Splits a quarter value back into its year and quarter number.
 *
 * @param value - A value made by {@link quarterValue}.
 * @returns The year and the quarter number as the API takes them, empty strings for a malformed value.
 */
export const parseQuarterValue = (value: string): { year: string; quarter: string } => {
  const [year = '', quarter = ''] = value.split('-')
  return { year, quarter }
}

/**
 * Builds the quarter options.
 *
 * @param now - The moment the options count back from; its year is the newest year offered.
 * @param years - How many calendar years, ending with the current one, to offer.
 * @param locale - Language of the short month names.
 * @returns One option per quarter, newest first, each carrying its year as `group`.
 */
export const buildQuarterOptions = (now: Date, years: number, locale: string): UiSelectOption<string>[] => {
  const monthName = new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' })
  const options: UiSelectOption<string>[] = []
  for (let year = now.getFullYear(); year > now.getFullYear() - years; year--) {
    for (let quarter = QUARTERS_PER_YEAR; quarter >= 1; quarter--) {
      const firstMonth = (quarter - 1) * MONTHS_PER_QUARTER
      const months = [firstMonth, firstMonth + MONTHS_PER_QUARTER - 1]
        .map(month => monthName.format(new Date(Date.UTC(year, month, 1))))
        .join(MONTH_RANGE_SEPARATOR)
      options.push({
        value: quarterValue(year, quarter),
        label: `${QUARTER_PREFIX}${quarter} ${year}${PART_SEPARATOR}${months}`,
        displayLabel: `${QUARTER_PREFIX}${quarter}${PART_SEPARATOR}${months}`,
        group: String(year),
      })
    }
  }
  return options
}
