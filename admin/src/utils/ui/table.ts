/**
 * Table vocabulary shared by `UiTable` and its parts: alignment, sort direction and the padding
 * classes a density maps to.
 */
import type { UiTableDensity } from '@/utils/ui/injection'

/** Closed set of horizontal alignments of a cell. */
export const UiAlign = {
  Left: 'left',
  Center: 'center',
  Right: 'right',
} as const

/** One of the alignments. */
export type UiAlign = (typeof UiAlign)[keyof typeof UiAlign]

/** Text alignment classes. */
export const alignClasses: Record<UiAlign, string> = {
  left: 'text-start',
  center: 'text-center',
  right: 'text-end',
}

/** Closed set of sort directions of a sortable column. */
export const UiSortDirection = {
  None: 'none',
  Ascending: 'ascending',
  Descending: 'descending',
} as const

/** One of the sort directions; also the value of `aria-sort`. */
export type UiSortDirection = (typeof UiSortDirection)[keyof typeof UiSortDirection]

/** Padding of a data cell per density. */
export const cellDensityClasses: Record<UiTableDensity, string> = {
  plain: '',
  compact: 'px-3 py-2',
  comfortable: 'px-4 py-3',
}

/** Padding of a header cell per density. */
export const headerDensityClasses: Record<UiTableDensity, string> = {
  plain: '',
  compact: 'px-3 py-2.5',
  comfortable: 'px-4 py-3',
}

/**
 * Classes of the actions column, header and cells alike: right aligned, one fixed width on every table
 * (wide enough for the kebab trigger or one labelled button) and never wrapped onto a second line.
 */
export const actionsColumnClasses = 'w-28 text-end whitespace-nowrap'

/** Classes of a numeric or money cell: right aligned, figures of one width, kept on one line. */
export const numericCellClasses = 'text-end tabular-nums whitespace-nowrap'
