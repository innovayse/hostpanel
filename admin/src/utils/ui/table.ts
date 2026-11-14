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

/** Closed set of roles a column plays when the table is drawn as cards (below the `sm` breakpoint). */
export const UiCardRole = {
  /** A label and value pair of the card, the value at the end of the line (the default). */
  Field: 'field',
  /** A label over a full width value: the role of a column whose cells hold a form control (an input, a textarea, a number input). */
  Control: 'control',
  /** The heading of the card. */
  Title: 'title',
  /** The chip at the top right of the card. */
  Badge: 'badge',
  /** Not drawn on the card. */
  Hidden: 'hidden',
} as const

/** One of the card roles. */
export type UiCardRole = (typeof UiCardRole)[keyof typeof UiCardRole]

/**
 * Below `sm` every table part is a block and a body row becomes a card: a wrapping flex box in which the
 * checkbox, the title and the badge share the first line and the fields and the actions take a line each.
 */
export const cardTableClasses = 'max-sm:block'

/** Classes of a body row drawn as a card; `last:` repeats the border because the row's own `last:border-0` would drop it. */
export const cardRowClasses =
  'max-sm:flex max-sm:flex-wrap max-sm:items-center max-sm:gap-x-3 max-sm:gap-y-1.5 max-sm:mb-2 max-sm:p-3 max-sm:rounded-xl max-sm:border max-sm:last:border max-sm:border-border-subtle'

/** Classes of a header row drawn as cards: only the select-all cell is left in it. */
export const cardHeadRowClasses = 'max-sm:flex'

/** Classes that drop a cell's table sizing (padding, height, fixed or zero width) once it is a card part. */
const cardCellResetClasses = 'max-sm:h-auto max-sm:p-0 max-sm:max-w-none'

/** Classes of the checkbox cell of a card: the first item of the first line. */
const cardSelectClasses = `${cardCellResetClasses} max-sm:block max-sm:order-1 max-sm:w-auto`

/** Classes of the title cell of a card: takes the first line's free width. */
const cardTitleClasses = `${cardCellResetClasses} max-sm:block max-sm:order-2 max-sm:flex-1 max-sm:min-w-0 max-sm:w-auto max-sm:font-semibold max-sm:text-text-primary max-sm:text-start`

/** Classes of the badge cell of a card: the end of the first line. */
const cardBadgeClasses = `${cardCellResetClasses} max-sm:block max-sm:order-3 max-sm:w-auto max-sm:text-end`

/** Classes of a field cell of a card: the column's caption at the start of the line, the value at the end. */
const cardFieldClasses = `${cardCellResetClasses} max-sm:order-4 max-sm:flex max-sm:w-full max-sm:items-center max-sm:justify-between max-sm:gap-3 max-sm:text-end`

/** Classes of a control cell of a card: the caption over the form control, which takes the full width of the card. */
const cardControlClasses = `${cardCellResetClasses} max-sm:order-4 max-sm:flex max-sm:w-full max-sm:flex-col max-sm:items-stretch max-sm:gap-1 max-sm:text-start`

/** Classes of the actions cell of a card: the footer line. */
const cardActionsClasses = `${cardCellResetClasses} max-sm:block max-sm:order-5 max-sm:w-full max-sm:pt-1.5 max-sm:border-t max-sm:border-border-subtle max-sm:text-end`

/** Classes of a hidden column: not drawn on the card, whatever its `hideBelow`. */
const cardHiddenClasses = 'max-sm:hidden'

/** Classes of a cell that spans the row (a `colspan` cell, or one with no header column of its own): a full width line of the card. */
const cardSpanClasses = `${cardCellResetClasses} max-sm:block max-sm:order-4 max-sm:w-full`

/** Classes of the caption a field or control cell draws before its value: real text, so it is read like the header it stands for. */
const cardVisibleLabelClasses = 'text-xs font-semibold text-text-muted text-start sm:hidden'

/** Classes of the caption a title or badge cell carries for assistive technology only. */
const cardHiddenLabelClasses = 'sr-only sm:hidden'

/**
 * The role a column plays on a card. A column that sets no `cardRole` is a field, except one that is hidden
 * below a breakpoint: it was left out of the narrow layout on purpose, so it stays out of the card too
 * unless its header says `card-role="field"` (or another role) explicitly.
 *
 * @param column - The column.
 * @returns The explicit role, else `hidden` for a `hideBelow` column, else `field`.
 */
export const cardRoleOf = (column: UiTableColumn): UiCardRole =>
  column.cardRole ?? (column.hideBelow ? UiCardRole.Hidden : UiCardRole.Field)

/**
 * Classes a column gives each of its cells, header, data and placeholder alike, once the table is drawn as cards.
 * The select and actions columns have fixed roles, any other follows its card role.
 *
 * @param column - The column of the cell, absent for a cell that matches none (a `colspan` cell): it becomes a full width line.
 * @returns The card layout classes of the cell.
 */
export const cardCellClasses = (column: UiTableColumn | undefined): string => {
  if (!column) {
    return cardSpanClasses
  }
  if (column.select) {
    return cardSelectClasses
  }
  if (column.actions) {
    return cardActionsClasses
  }
  switch (cardRoleOf(column)) {
    case UiCardRole.Title:
      return cardTitleClasses
    case UiCardRole.Badge:
      return cardBadgeClasses
    case UiCardRole.Control:
      return cardControlClasses
    case UiCardRole.Hidden:
      return cardHiddenClasses
    default:
      return cardFieldClasses
  }
}

/**
 * Classes of the caption element a cell draws for its column on a card, hidden from `sm` up where the header says it.
 * Fields and controls show it, a title or badge carries it for assistive technology, and the select, actions and
 * hidden columns have none (the checkbox and the buttons name themselves).
 *
 * @param column - The column of the cell, absent for a cell that matches none.
 * @returns The classes, or an empty string when the cell draws no caption.
 */
export const cardLabelClasses = (column: UiTableColumn | undefined): string => {
  if (!column || column.select || column.actions || !column.label) {
    return ''
  }
  switch (cardRoleOf(column)) {
    case UiCardRole.Field:
    case UiCardRole.Control:
      return cardVisibleLabelClasses
    case UiCardRole.Title:
    case UiCardRole.Badge:
      return cardHiddenLabelClasses
    default:
      return ''
  }
}

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
