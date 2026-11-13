/**
 * Table vocabulary shared by `UiTable` and its parts: alignment, sort direction and the padding
 * classes a density maps to.
 */
import type { UiTableColumn, UiTableDensity } from '@/utils/ui/injection'

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

/** Closed set of breakpoints below which a column is hidden. */
export const UiHideBelow = {
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
  Xl: 'xl',
} as const

/** One of the breakpoints. */
export type UiHideBelow = (typeof UiHideBelow)[keyof typeof UiHideBelow]

/** Classes that hide a header or data cell below a breakpoint and show it as a table cell from there. */
export const hideBelowClasses: Record<UiHideBelow, string> = {
  sm: 'hidden sm:table-cell',
  md: 'hidden md:table-cell',
  lg: 'hidden lg:table-cell',
  xl: 'hidden xl:table-cell',
}

/** Closed set of sort directions of a sortable column. */
export const UiSortDirection = {
  None: 'none',
  Ascending: 'ascending',
  Descending: 'descending',
} as const

/** One of the sort directions; also the value of `aria-sort`. */
export type UiSortDirection = (typeof UiSortDirection)[keyof typeof UiSortDirection]

/** Closed set of heights at which a table starts scrolling vertically inside its own box. */
export const UiTableMaxHeight = {
  Sm: 'sm',
  Md: 'md',
  Lg: 'lg',
} as const

/** One of the table box heights. */
export type UiTableMaxHeight = (typeof UiTableMaxHeight)[keyof typeof UiTableMaxHeight]

/**
 * Fill of a sticky header. A header that sticks has rows scrolling under it, so it needs the dense frosted
 * fill; a header that does not stick stays transparent and shows the card behind it, as in the concept.
 * A table without a scrolling box of its own (a page-level list) gets it too, because its header sticks to
 * the page's scroll container.
 */
export const stickyHeadFill = '[&_thead]:bg-(--glass-strong-bg)'

/** The sideways scrolling box of a table: the global thin scrollbar, lifted off the card edge. */
export const scrollBoxClasses = 'overflow-x-auto mb-1.5'

/** Height cap per size; the box scrolls vertically past it, so the sticky header has something to stick to. */
export const tableMaxHeightClasses: Record<UiTableMaxHeight, string> = {
  sm: `max-h-80 overflow-y-auto ${stickyHeadFill}`,
  md: `max-h-[28rem] overflow-y-auto ${stickyHeadFill}`,
  lg: `max-h-[40rem] overflow-y-auto ${stickyHeadFill}`,
}

/**
 * Box of a data cell per density: the 14px body text and a 48px row (40px compact), as in the concept's
 * invoice list. The height is a minimum, so a two-line cell grows its row instead of being clipped.
 */
export const cellDensityClasses: Record<UiTableDensity, string> = {
  plain: '',
  compact: 'h-10 px-3 py-1.5 text-sm',
  comfortable: 'h-12 px-3 py-2 text-sm sm:px-4',
}

/** Box of a header cell per density: a 40px caption row (36px compact) over the same horizontal padding as the cells. */
export const headerDensityClasses: Record<UiTableDensity, string> = {
  plain: '',
  compact: 'h-9 px-3',
  comfortable: 'h-10 px-3 sm:px-4',
}

/**
 * Classes of the actions column, header and cells alike: right aligned, one fixed width on every table
 * (a kebab trigger on a phone, wide enough for one labelled button from `sm`) and never wrapped onto a second line.
 */
export const actionsColumnClasses = 'w-16 sm:w-28 text-end whitespace-nowrap'

/**
 * Classes of the one cell of a row that takes the remaining width and lets its content truncate (a name): the
 * zero max width stops the content from widening the column, the full width gives it what the other columns leave.
 */
export const truncateCellClasses = 'w-full max-w-0'

/** Classes of a numeric or money cell: right aligned, figures of one width, kept on one line. */
export const numericCellClasses = 'text-end tabular-nums whitespace-nowrap'

/** Classes of the checkbox column of a selectable table, header and cells alike: narrow, and the box is never selected as text by a Shift range. */
export const selectColumnClasses = 'w-12 select-none'

/**
 * Classes of a selected row: a soft accent background and a thin accent bar on the left edge (an inset shadow of
 * the first cell, because a border or shadow on a `<tr>` does not paint in a collapsed-border table).
 */
export const selectedRowClasses = 'bg-primary-500/10 [&>:first-child]:shadow-[inset_3px_0_0_var(--text-accent)]'

/**
 * Classes a column gives each of its cells, header, data and placeholder alike: the select, actions and numeric shapes
 * win over `align`, then the breakpoint below which the column is hidden.
 *
 * @param column - The column whose cells are styled.
 * @returns The alignment shape class and the hide-below class (empty when the column is always shown).
 */
export const columnClasses = (column: UiTableColumn): string[] => {
  let shape: string = alignClasses[column.align ?? UiAlign.Left]
  if (column.select) {
    shape = selectColumnClasses
  } else if (column.actions) {
    shape = actionsColumnClasses
  } else if (column.numeric) {
    shape = numericCellClasses
  }
  return [shape, column.hideBelow ? hideBelowClasses[column.hideBelow] : '']
}
