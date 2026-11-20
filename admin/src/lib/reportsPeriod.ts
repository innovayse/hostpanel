/**
 * Month and year choices for the report filters.
 */
import { translate } from '@/lib/translate'

/** Closed set of the filter keys a report schema uses for its period dropdowns, which `ReportSchemaFilters` draws itself. */
export const ReportPeriodField = {
  /** The month of a year, 1 to 12. */
  Month: 'month',
  /** The year. */
  Year: 'year',
  /** The currency the figures are shown in. */
  Currency: 'currency',
} as const

/** One of the period filter keys. */
export type ReportPeriodField = (typeof ReportPeriodField)[keyof typeof ReportPeriodField]

/** One choice of a period dropdown. */
export interface ReportPeriodOption {
  /** Month number (1 to 12) or year. */
  value: number
  /** Text shown in the dropdown. */
  label: string
}

/**
 * Builds the twelve months, named in the active language.
 *
 * @returns One option per month, January first.
 */
export const monthOptions = (): ReportPeriodOption[] =>
  Array.from({ length: 12 }, (_, index) => ({ value: index + 1, label: translate(`calendar.months.${index + 1}`) }))

/**
 * Builds a list of years around the current one, newest first.
 *
 * @param back - How many years before the current one to offer.
 * @param ahead - How many years after the current one to offer.
 * @returns One option per year.
 */
export const yearOptions = (back: number, ahead = 0): ReportPeriodOption[] => {
  const current = new Date().getFullYear()
  return Array.from({ length: back + ahead + 1 }, (_, index) => {
    const year = current + ahead - index
    return { value: year, label: String(year) }
  })
}

/**
 * Names a month of the calendar in the active language.
 *
 * @param month - Month number, 1 to 12.
 * @returns The month name.
 */
export const monthName = (month: number): string => translate(`calendar.months.${month}`)

/**
 * The current calendar year, the default period of a report.
 *
 * @returns The four-digit year.
 */
export const currentYear = (): number => new Date().getFullYear()

/**
 * The current month number, the default month of a monthly report.
 *
 * @returns The month, 1 to 12.
 */
export const currentMonthNumber = (): number => new Date().getMonth() + 1
