/**
 * Date vocabulary shared by `UiDatePicker` and `UiDateRangePicker`.
 *
 * Both pickers speak ISO `YYYY-MM-DD` on the model, show `DD/MM/YYYY` to the reader, accept the
 * same typed forms and draw the same month grid. The pure parts live here so the two components
 * cannot drift apart; every colour in the class sets is a theme token, so the popups follow the
 * light/dark switch.
 */
import { FLOATING_PANEL_CLASSES } from '@/utils/ui/floating'
import { fieldBaseClasses, fieldWidthClasses, focusRingClasses } from '@/utils/ui/input'
import { fieldSizeClasses } from '@/utils/ui/size'

/** Which way the quick choices of a single-date picker point from today. */
export const UiDatePickerDirection = {
  /** Quick choices look back: yesterday, 7 days ago, 1 month ago, 1 year ago. */
  Past: 'past',
  /** Quick choices look ahead: tomorrow, in 7 days, 1 month from now, 1 year from now. */
  Future: 'future',
} as const

/** One of the quick-choice directions. */
export type UiDatePickerDirection = (typeof UiDatePickerDirection)[keyof typeof UiDatePickerDirection]

/** Closed set of quick choices in the left column of the single-date popup. */
export const QuickDate = {
  Today: 'today',
  Day: 'day',
  Week: 'week',
  Month: 'month',
  Year: 'year',
  Custom: 'custom',
} as const

/** One of the quick choices. */
export type QuickDate = (typeof QuickDate)[keyof typeof QuickDate]

/** Quick choices in the order the popup lists them. */
export const quickDateOrder: readonly QuickDate[] = [
  QuickDate.Today,
  QuickDate.Day,
  QuickDate.Week,
  QuickDate.Month,
  QuickDate.Year,
  QuickDate.Custom,
]

/** Locale key of every quick choice's caption, per direction. */
export const quickDateLabelKeys: Record<UiDatePickerDirection, Record<QuickDate, string>> = {
  past: {
    today: 'shared.today',
    day: 'ui.uiDatePicker.yesterday',
    week: 'ui.uiDatePicker.7DaysAgo',
    month: 'ui.uiDatePicker.1MonthAgo',
    year: 'ui.uiDatePicker.1YearAgo',
    custom: 'shared.custom',
  },
  future: {
    today: 'shared.today',
    day: 'ui.uiDatePickerFuture.tomorrow',
    week: 'ui.uiDatePickerFuture.in7Days',
    month: 'ui.uiDatePickerFuture.1MonthFromNow',
    year: 'ui.uiDatePickerFuture.1YearFromNow',
    custom: 'shared.custom',
  },
}

/** Which edge of a range a calendar day is. */
export const RangeEdge = {
  Start: 'start',
  End: 'end',
} as const

/** One of the range edges. */
export type RangeEdge = (typeof RangeEdge)[keyof typeof RangeEdge]

/** First year the month/year selects offer. */
export const FIRST_PICKER_YEAR = 2020

/** Last year the month/year selects offer. */
export const LAST_PICKER_YEAR = 2030

/** Earliest year a typed date may carry. */
export const MIN_TYPED_YEAR = 2000

/** Latest year a partially typed date may carry. */
export const MAX_PARTIAL_YEAR = 2099

/** Digits in a fully typed `DDMMYYYY` date. */
export const TYPED_DATE_LENGTH = 8

/** Parts of a date the reader has typed so far. */
export interface PartialDate {
  /** Day of month, 1-31, once two digits (or one valid digit) are in. */
  day?: number
  /** Month, 1-12, once the third digit is in. */
  month?: number
  /** Four-digit year within the typed range. */
  year?: number
}

/** A select option of the month/year dropdowns. */
export interface DateSelectOption {
  /** Value the select holds. */
  value: string
  /** Caption the reader sees. */
  label: string
}

/**
 * Retints the date-picker library (its `--dp-*` custom properties) with the panel tokens, so its
 * input, calendar and time wheel follow the light/dark switch and the control radius. The selector
 * `.cls .dp--main` outranks the library's own `.dp--theme-dark` / `.dp--theme-light` rule that declares
 * the same variables on the same element. Written out literally so Tailwind emits every class.
 */
export const dateLibraryThemeClasses =
  '[&_.dp--main]:[--dp-background-color:var(--surface-elevated)] [&_.dp--main]:[--dp-text-color:var(--text-primary)] [&_.dp--main]:[--dp-hover-color:var(--surface-hover)] [&_.dp--main]:[--dp-hover-text-color:var(--text-primary)] [&_.dp--main]:[--dp-hover-icon-color:var(--text-primary)] [&_.dp--main]:[--dp-primary-color:var(--color-primary-500)] [&_.dp--main]:[--dp-primary-disabled-color:var(--border-strong)] [&_.dp--main]:[--dp-primary-text-color:var(--surface-base)] [&_.dp--main]:[--dp-secondary-color:var(--text-muted)] [&_.dp--main]:[--dp-border-color:var(--border)] [&_.dp--main]:[--dp-menu-border-color:var(--border-strong)] [&_.dp--main]:[--dp-border-color-hover:var(--text-secondary)] [&_.dp--main]:[--dp-border-color-focus:var(--text-accent)] [&_.dp--main]:[--dp-disabled-color:var(--surface-hover)] [&_.dp--main]:[--dp-disabled-color-text:var(--text-muted)] [&_.dp--main]:[--dp-scroll-bar-background:var(--surface-elevated)] [&_.dp--main]:[--dp-scroll-bar-color:var(--border-strong)] [&_.dp--main]:[--dp-icon-color:var(--text-secondary)] [&_.dp--main]:[--dp-danger-color:var(--color-status-red)] [&_.dp--main]:[--dp-highlight-color:var(--tint-blue)] [&_.dp--main]:[--dp-tooltip-color:var(--surface-hover)] [&_.dp--main]:[--dp-border-radius:var(--radius-control)] [&_.dp--main]:[--dp-font-family:var(--font-sans)]'

/** Classes of the text field that opens a popup. */
export const dateFieldClasses = `${fieldWidthClasses} ${fieldBaseClasses} ${fieldSizeClasses.md}`

/** Classes of the popup panel (the position is added by the component); opaque, so nothing shows through it. */
export const datePopupClasses = `${FLOATING_PANEL_CLASSES} rounded-card`

/** Classes of the previous/next month arrows. */
export const dateArrowClasses = `rounded-lg px-2 text-text-secondary hover:text-text-primary text-lg transition-colors ${focusRingClasses}`

/** Classes of a plain text action (Clear, Close). */
export const dateActionClasses = `rounded-lg text-sm text-text-secondary hover:text-text-primary transition-colors ${focusRingClasses}`

/** Classes of one calendar cell, common to every state. */
export const dateCellBaseClasses = `h-8 flex items-center justify-center text-sm rounded-lg transition-colors ${focusRingClasses}`

/** Classes of the selected day (and of a range edge). */
export const dateCellSelectedClasses = 'bg-primary-400 light:bg-primary-600 text-surface-base rounded-lg font-semibold'

/** Classes of a day inside a range. */
export const dateCellInRangeClasses = 'bg-primary-500/25 text-text-primary'

/** Classes of an empty padding cell before the first of the month. */
export const dateCellEmptyClasses = 'text-text-muted opacity-60 cursor-not-allowed'

/** Classes of a selectable, unselected day. */
export const dateCellIdleClasses = 'text-text-primary hover:bg-surface-hover'

/** Classes of a preset/quick-choice button in its idle state. */
export const datePresetIdleClasses = `text-text-secondary hover:text-text-primary hover:bg-surface-hover ${focusRingClasses}`

/** Classes of a quick-choice button in its active state. */
export const datePresetActiveClasses = 'bg-primary-400 light:bg-primary-600 text-surface-base'

/**
 * Renders a local date as `YYYY-MM-DD`.
 *
 * @param date - The date to render.
 * @returns The ISO day in local time.
 */
export const formatIsoDate = (date: Date): string => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * Renders an ISO day the way the reader types it.
 *
 * @param iso - Date in `YYYY-MM-DD` form.
 * @returns The same day as `DD/MM/YYYY`.
 */
export const formatDisplayDate = (iso: string): string => {
  const [year, month, day] = iso.split('-')
  return `${day}/${month}/${year}`
}

/**
 * Reads a complete typed date (`DD/MM/YYYY`, `DDMMYYYY`, any separators).
 *
 * @param input - What the reader typed.
 * @returns The ISO day, or null while the input is incomplete or not a date.
 */
export const parseTypedDate = (input: string): string | null => {
  const cleaned = input.replace(/\D/g, '')
  if (cleaned.length !== TYPED_DATE_LENGTH) {
    return null
  }
  const day = parseInt(cleaned.substring(0, 2))
  const month = parseInt(cleaned.substring(2, 4))
  const year = parseInt(cleaned.substring(4, 8))
  if (day < 1 || day > 31 || month < 1 || month > 12 || year < MIN_TYPED_YEAR) {
    return null
  }
  const iso = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`
  return Number.isNaN(new Date(iso).getTime()) ? null : iso
}

/**
 * Reads whatever parts of a date the reader has typed so far, so the calendar can follow along.
 *
 * @param input - The partially typed text.
 * @returns The day, month and year that are already valid.
 */
export const parsePartialDate = (input: string): PartialDate => {
  const cleaned = input.replace(/\D/g, '')
  const result: PartialDate = {}

  if (cleaned.length >= 1) {
    const day = parseInt(cleaned.substring(0, 2))
    if (day >= 1 && day <= 31) {
      result.day = day
    }
  }
  if (cleaned.length >= 3) {
    const month = parseInt(cleaned.substring(2, 4))
    if (month >= 1 && month <= 12) {
      result.month = month
    }
  }
  if (cleaned.length >= 4) {
    const year = parseInt(cleaned.substring(4, 8))
    if (year >= MIN_TYPED_YEAR && year <= MAX_PARTIAL_YEAR) {
      result.year = year
    }
  }
  return result
}

/**
 * Turns a partial date into a local date when all three parts are known.
 *
 * @param partial - The parts typed so far.
 * @returns The date at local midnight, or null while a part is missing.
 */
export const partialToDate = (partial: PartialDate): Date | null => {
  if (!partial.day || !partial.month || !partial.year) {
    return null
  }
  return new Date(partial.year, partial.month - 1, partial.day)
}

/**
 * Lists the cells of a month grid: leading nulls for the weekday offset, then 1..N.
 *
 * @param date - Any date inside the month.
 * @returns One entry per grid cell; null is padding.
 */
export const getDaysForMonth = (date: Date): (number | null)[] => {
  const days: (number | null)[] = []
  const firstDay = new Date(date.getFullYear(), date.getMonth(), 1).getDay()
  for (let i = 0; i < firstDay; i++) {
    days.push(null)
  }
  const count = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate()
  for (let i = 1; i <= count; i++) {
    days.push(i)
  }
  return days
}

/**
 * Lists a stable key for each cell of a month grid: the ISO day the cell sits on. A padding cell sits
 * on a day of the neighbouring month, so every key in the grid is unique.
 *
 * @param date - Any date inside the month.
 * @returns One ISO day per cell of {@link getDaysForMonth}, in the same order.
 */
export const getDayKeysForMonth = (date: Date): string[] => {
  const firstDay = new Date(date.getFullYear(), date.getMonth(), 1).getDay()
  const cellCount = getDaysForMonth(date).length
  return Array.from({ length: cellCount }, (_, cell) => formatIsoDate(new Date(date.getFullYear(), date.getMonth(), 1 - firstDay + cell)))
}

/**
 * Builds the year dropdown options.
 *
 * @returns Every year from the first to the last picker year.
 */
export const buildYearOptions = (): DateSelectOption[] => {
  const years: DateSelectOption[] = []
  for (let year = FIRST_PICKER_YEAR; year <= LAST_PICKER_YEAR; year++) {
    years.push({ value: String(year), label: String(year) })
  }
  return years
}

/**
 * Resolves a quick choice to the day it stands for.
 *
 * @param choice - The quick choice.
 * @param direction - Whether the offset counts back from today or forward.
 * @param today - Reference day; injectable for tests.
 * @returns The chosen day (today for `Today` and `Custom`).
 */
export const quickDateFor = (
  choice: QuickDate,
  direction: UiDatePickerDirection,
  today: Date = new Date(),
): Date => {
  const sign = direction === UiDatePickerDirection.Past ? -1 : 1
  const date = new Date(today)
  if (choice === QuickDate.Day) {
    date.setDate(date.getDate() + sign)
  } else if (choice === QuickDate.Week) {
    date.setDate(date.getDate() + sign * 7)
  } else if (choice === QuickDate.Month) {
    date.setMonth(date.getMonth() + sign)
  } else if (choice === QuickDate.Year) {
    date.setFullYear(date.getFullYear() + sign)
  }
  return date
}

/**
 * Tells which quick choice a stored day matches.
 *
 * @param iso - The stored ISO day, or null.
 * @param direction - Quick-choice direction.
 * @param today - Reference day; injectable for tests.
 * @returns The matching choice, `Custom` when none match, null when nothing is stored.
 */
export const matchQuickDate = (
  iso: string | null,
  direction: UiDatePickerDirection,
  today: Date = new Date(),
): QuickDate | null => {
  if (!iso) {
    return null
  }
  const candidates = [QuickDate.Today, QuickDate.Day, QuickDate.Week, QuickDate.Month, QuickDate.Year]
  const hit = candidates.find(choice => formatIsoDate(quickDateFor(choice, direction, today)) === iso)
  return hit ?? QuickDate.Custom
}

/** Closed set of presets in the left column of the range popup. */
export const DateRangePreset = {
  Today: 'today',
  Yesterday: 'yesterday',
  Last7Days: 'last7Days',
  Last30Days: 'last30Days',
  ThisMonth: 'thisMonth',
  LastMonth: 'lastMonth',
  ThisYear: 'thisYear',
  LastYear: 'lastYear',
} as const

/** One of the range presets. */
export type DateRangePreset = (typeof DateRangePreset)[keyof typeof DateRangePreset]

/** Presets in the order the popup lists them, with the locale key of each caption. */
export const dateRangePresets: readonly { preset: DateRangePreset; labelKey: string }[] = [
  { preset: DateRangePreset.Today, labelKey: 'shared.today' },
  { preset: DateRangePreset.Yesterday, labelKey: 'ui.uiDateRangePicker.yesterday' },
  { preset: DateRangePreset.Last7Days, labelKey: 'ui.uiDateRangePicker.last7Days' },
  { preset: DateRangePreset.Last30Days, labelKey: 'ui.uiDateRangePicker.last30Days' },
  { preset: DateRangePreset.ThisMonth, labelKey: 'ui.uiDateRangePicker.thisMonth' },
  { preset: DateRangePreset.LastMonth, labelKey: 'ui.uiDateRangePicker.1MonthAgo' },
  { preset: DateRangePreset.ThisYear, labelKey: 'ui.uiDateRangePicker.thisYear' },
  { preset: DateRangePreset.LastYear, labelKey: 'ui.uiDateRangePicker.1YearAgo' },
]

/**
 * Resolves a range preset to its first and last day.
 *
 * @param preset - The preset.
 * @param today - Reference day; injectable for tests.
 * @returns The start and end of the range.
 */
export const presetRange = (preset: DateRangePreset, today: Date = new Date()): [Date, Date] => {
  const year = today.getFullYear()
  const month = today.getMonth()
  /**
   * Range ending today and starting the given number of days earlier.
   *
   * @param days - How many days before today the range starts.
   * @returns The start and end dates.
   */
  const daysBack = (days: number): [Date, Date] => {
    const start = new Date(today)
    start.setDate(start.getDate() - days)
    return [start, new Date(today)]
  }
  switch (preset) {
    case DateRangePreset.Yesterday:
      return daysBack(1)
    case DateRangePreset.Last7Days:
      return daysBack(6)
    case DateRangePreset.Last30Days:
      return daysBack(29)
    case DateRangePreset.ThisMonth:
      return [new Date(year, month, 1), new Date(today)]
    case DateRangePreset.LastMonth:
      return [new Date(year, month - 1, 1), new Date(year, month, 0)]
    case DateRangePreset.ThisYear:
      return [new Date(year, 0, 1), new Date(today)]
    case DateRangePreset.LastYear:
      return [new Date(year - 1, 0, 1), new Date(year - 1, 11, 31)]
    default:
      return daysBack(0)
  }
}
