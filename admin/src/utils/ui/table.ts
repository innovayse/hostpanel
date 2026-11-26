/**
 * Table vocabulary shared by `UiTable` and its parts: alignment, sort direction and the padding
 * classes a density maps to.
 */
import type { UiRecordCardSkeleton } from '@/utils/ui/card'
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
  TwoXl: '2xl',
} as const

/** One of the breakpoints. */
export type UiHideBelow = (typeof UiHideBelow)[keyof typeof UiHideBelow]

/** Classes that hide a header or data cell below a breakpoint and show it as a table cell from there. */
export const hideBelowClasses: Record<UiHideBelow, string> = {
  sm: 'hidden sm:table-cell',
  md: 'hidden md:table-cell',
  lg: 'hidden lg:table-cell',
  xl: 'hidden xl:table-cell',
  '2xl': 'hidden 2xl:table-cell',
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
 * Classes of the actions column, header and cells alike: right aligned and never wrapped onto a second line. Its width
 * is the `Compact` size every actions column takes (see {@link columnSizeOf}): one icon button or one "…" trigger.
 */
export const actionsColumnClasses = 'text-end whitespace-nowrap'

/** Closed set of widths a column takes in the table. */
export const UiColumnSize = {
  /** Shrinks to its content, on one line: the width of an id, a status, a date, an icon. */
  Compact: 'compact',
  /** Its content on one line, as wide as the content needs, never truncated (an address that must stay whole). */
  Auto: 'auto',
  /** Takes a double share of the table's width (the share of every data column, so the free width is spread evenly) and truncates its content with a tooltip (a name, an email, a title). */
  Grow: 'grow',
  /** Takes a single share of the table's width, on one line: a status, a date, an id, a number. */
  Narrow: 'narrow',
  /** A width of its own, one of {@link UiColumnWidth}, on one line. */
  Fixed: 'fixed',
} as const

/** One of the column sizes. */
export type UiColumnSize = (typeof UiColumnSize)[keyof typeof UiColumnSize]

/** Closed set of kinds of data a column holds, which decide its width when the screen names no size. */
export const UiColumnKind = {
  /** Free text (a name, an email, a title): takes a double share of the width and truncates (the default). */
  Text: 'text',
  /** A status, a badge or an icon: takes a single share of the width. */
  Status: 'status',
  /** A date or a time: takes a single share of the width. */
  Date: 'date',
  /** An id or a short code: takes a single share of the width. */
  Id: 'id',
  /** A thumbnail (a logo, a cover, a picture): the one fixed width every image column shares, so it never takes a share of the row. */
  Image: 'image',
} as const

/** One of the column kinds. */
export type UiColumnKind = (typeof UiColumnKind)[keyof typeof UiColumnKind]

/** The size a column of each kind takes when the screen names none. */
export const kindSizes: Record<UiColumnKind, UiColumnSize> = {
  text: UiColumnSize.Grow,
  status: UiColumnSize.Narrow,
  date: UiColumnSize.Narrow,
  id: UiColumnSize.Narrow,
  image: UiColumnSize.Fixed,
}

/** Closed set of widths a `Fixed` column takes. */
export const UiColumnWidth = {
  /** A narrow column (a flag, a short figure). */
  Small: 'small',
  /** A medium column. */
  Medium: 'medium',
  /** A wide column. */
  Large: 'large',
} as const

/** One of the fixed column widths. */
export type UiColumnWidth = (typeof UiColumnWidth)[keyof typeof UiColumnWidth]

/** The width a `Fixed` column of each kind takes when the screen names none; a kind not listed takes `Medium`. */
export const kindWidths: Partial<Record<UiColumnKind, UiColumnWidth>> = {
  image: UiColumnWidth.Small,
}

/** Classes per fixed width, literal so Tailwind emits them. */
export const columnWidthClasses: Record<UiColumnWidth, string> = {
  small: 'w-24',
  medium: 'w-40',
  large: 'w-64',
}

/**
 * Classes per column size, header and cells alike. No size wraps its text: a `Compact` column shrinks to its
 * content (an icon, the actions), an `Auto` one keeps it on a line, a `Fixed` one is as wide as its width token. A
 * `Grow` and a `Narrow` column claim a share of the table's width in percent, two parts for the text and one for the rest, so when the
 * table is wider than its content the browser spreads the free width over every data column in those proportions instead of
 * handing it to one column; the table never grows past its box, because shares over 100% are scaled down. `Grow` also gets
 * a zero max width, which stops its content from widening the column, so the content truncates instead, and a minimum width
 * that keeps it readable. A minimum width (which wins over
 * the zero maximum) keeps the column readable when its neighbours need the room, so a narrow screen scrolls the table
 * sideways rather than squeezing the name to a letter or two.
 */
export const columnSizeClasses: Record<UiColumnSize, string> = {
  compact: 'w-px whitespace-nowrap',
  auto: 'whitespace-nowrap',
  grow: 'w-[28%] max-w-0 min-w-36 truncate',
  narrow: 'w-[14%] min-w-24 whitespace-nowrap',
  fixed: 'whitespace-nowrap',
}

/**
 * Classes of a `Grow` column whose cells hold a form control: the full width the others leave, and no truncation, because
 * `overflow: hidden` on the cell would clip the control's focus ring.
 */
export const growControlClasses = 'w-full'

/**
 * The size a column takes: an actions column and the checkbox column of a selectable table are always `Compact` (one icon
 * button, one "…" trigger or one checkbox, never a share of the row), a size the screen
 * names wins, and any other column takes the size of its kind, a number that of a status (free text a double share, the
 * rest a single one).
 *
 * @param column - The column.
 * @returns Its size.
 */
export const sizeOfColumn = (column: UiTableColumn): UiColumnSize => {
  if (column.actions || column.select) {
    return UiColumnSize.Compact
  }
  if (column.size !== undefined) {
    return column.size
  }
  return column.numeric ? UiColumnSize.Narrow : kindSizes[column.kind ?? UiColumnKind.Text]
}

/**
 * Classes a column's size gives its header, cells and placeholder cells.
 *
 * @param column - The column.
 * @returns The classes of its size; a growing column of controls takes the width but never truncates.
 */
export const columnSizeOf = (column: UiTableColumn): string => {
  const size = sizeOfColumn(column)
  if (size === UiColumnSize.Grow && column.cardRole === UiCardRole.Control) {
    return growControlClasses
  }
  if (size === UiColumnSize.Fixed) {
    const width = column.width ?? (column.kind ? kindWidths[column.kind] : undefined) ?? UiColumnWidth.Medium
    return `${columnSizeClasses.fixed} ${columnWidthClasses[width]}`
  }
  return columnSizeClasses[size]
}

/**
 * Whether the cells of a column truncate their content, so a cell shows the full text in a tooltip when it is cut.
 *
 * @param column - The column of the cell.
 * @returns True for a `Grow` column that holds text (not a form control).
 */
export const truncatesColumn = (column: UiTableColumn | undefined): boolean =>
  column !== undefined && sizeOfColumn(column) === UiColumnSize.Grow && column.cardRole !== UiCardRole.Control

/** Classes of a numeric or money cell: right aligned, figures of one width, kept on one line. */
export const numericCellClasses = 'text-end tabular-nums whitespace-nowrap'

/**
 * Classes of the checkbox column of a selectable table, header and cells alike: the box is never selected as text by a
 * Shift range. Its width is the `Compact` size every select column takes (see {@link sizeOfColumn}), just the checkbox
 * and the cell padding, so the first data column starts next to it.
 */
export const selectColumnClasses = 'select-none'

/**
 * Classes of a selected row: a soft accent background and a thin accent bar on the left edge (an inset shadow of
 * the first cell, because a border or shadow on a `<tr>` does not paint in a collapsed-border table).
 */
export const selectedRowClasses = 'bg-primary-500/10 [&>:first-child]:shadow-[inset_3px_0_0_var(--text-accent)]'

/** Closed set of roles a column plays when the table is drawn as a list of cards (below the `sm` breakpoint). */
export const UiCardRole = {
  /** A label and value pair of the card, the value at the end of the line (the default). */
  Field: 'field',
  /** A label over a full width value: the role of a column whose cells hold a form control (an input, a textarea, a number input). */
  Control: 'control',
  /** The heading of the card. */
  Title: 'title',
  /** A muted line under the heading: the secondary value of the record (an email under a name). */
  Subtitle: 'subtitle',
  /** The chip at the top right of the card. */
  Badge: 'badge',
  /** Not drawn on the card. */
  Hidden: 'hidden',
} as const

/** One of the card roles. */
export type UiCardRole = (typeof UiCardRole)[keyof typeof UiCardRole]

/** Classes of a field cell of a card: the column's caption at the start of the line, the value at the end. */
const cardFieldClasses = 'flex w-full items-center justify-between gap-3 text-end'

/** Classes of a control cell of a card: the caption over the form control, which takes the full width of the card. */
const cardControlClasses = 'flex w-full flex-col items-stretch gap-1 text-start'

/** Classes of a cell that spans the row (a `colspan` cell, or one with no header column of its own): a full width line of the card. */
const cardSpanClasses = 'w-full'

/** Classes of the caption a field or control cell draws before its value: real text, so it is read like the header it stands for. */
const cardVisibleLabelClasses = 'text-xs font-semibold text-text-muted text-start'

/** Classes of the caption a heading, subtitle or badge cell carries for assistive technology only. */
const cardHiddenLabelClasses = 'sr-only'

/**
 * The role a column plays on a card: the one its header names, else a captioned field, whatever breakpoint the column is
 * hidden below in the wide table (a phone has the room a narrow desktop lacks). The heading is picked by {@link withCardTitle}.
 *
 * @param column - The column.
 * @returns The explicit role, else `field`.
 */
export const cardRoleOf = (column: UiTableColumn): UiCardRole => column.cardRole ?? UiCardRole.Field

/**
 * Gives a table's columns their card heading and badge. A table whose headers name no heading gets one by default: its `Grow` column
 * (the one that names the record), else its first column that is neither the checkbox nor the actions; its first `Status` column
 * is the badge at the top right, so a screen writes `card-role` only for a subtitle, a control or a column to leave out. A column that names its role is never changed,
 * and a table whose columns all name theirs (an editable table of controls) has no heading.
 *
 * @param columns - The columns of one header row, in order.
 * @returns The same columns, the heading one carrying `title` as its role.
 */
export const withCardTitle = (columns: readonly UiTableColumn[]): UiTableColumn[] => {
  const candidates = columns.filter((column) => column.cardRole === undefined && !column.select && !column.actions)
  const named = columns.some((column) => column.cardRole === UiCardRole.Title)
  const title = named ? undefined : (candidates.find((column) => column.size === UiColumnSize.Grow) ?? candidates[0])
  // The first status column is the badge at the top right of the card, unless a column already names that role.
  const badged = columns.some((column) => column.cardRole === UiCardRole.Badge)
  const badge = badged ? undefined : candidates.find((column) => column !== title && column.kind === UiColumnKind.Status)
  return columns.map((column) => {
    if (column === title) {
      return { ...column, cardRole: UiCardRole.Title }
    }
    return column === badge ? { ...column, cardRole: UiCardRole.Badge } : column
  })
}

/**
 * Classes of the box a cell is drawn in on a card. The heading, subtitle, badge and actions cells have none, because the
 * card places them itself; a field and a control are lines of the card's body.
 *
 * @param column - The column of the cell, absent for a cell that matches none (a `colspan` cell): it becomes a full width line.
 * @returns The classes of the cell's box, empty when the card places it.
 */
export const cardCellClasses = (column: UiTableColumn | undefined): string => {
  if (!column) {
    return cardSpanClasses
  }
  if (column.select || column.actions) {
    return ''
  }
  switch (cardRoleOf(column)) {
    case UiCardRole.Control:
      return cardControlClasses
    case UiCardRole.Field:
      return cardFieldClasses
    default:
      return ''
  }
}

/**
 * Classes of the caption element a cell draws for its column on a card. Fields and controls show it, a heading, subtitle or
 * badge carries it for assistive technology, and the select, actions and hidden columns have none (the checkbox and the
 * buttons name themselves).
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
    case UiCardRole.Subtitle:
    case UiCardRole.Badge:
      return cardHiddenLabelClasses
    default:
      return ''
  }
}

/**
 * The shape of the loading card a table's columns describe: which parts of the card exist and how many captioned fields it has.
 *
 * @param columns - The columns of the header row, card heading resolved (see {@link withCardTitle}).
 * @returns The skeleton shape the card list draws its placeholder cards in.
 */
export const cardSkeletonOf = (columns: readonly UiTableColumn[]): UiRecordCardSkeleton => {
  /**
   * Whether a data column of the row plays a role on the card.
   *
   * @param role - The card role to look for.
   * @returns True when a column plays it.
   */
  const has = (role: UiCardRole): boolean => columns.some((column) => !column.select && !column.actions && cardRoleOf(column) === role)
  return {
    select: columns.some((column) => column.select),
    subtitle: has(UiCardRole.Subtitle),
    badge: has(UiCardRole.Badge),
    actions: columns.some((column) => column.actions),
    fields: columns.filter((column) => !column.select && !column.actions && ([UiCardRole.Field, UiCardRole.Control] as readonly UiCardRole[]).includes(cardRoleOf(column))).length,
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
