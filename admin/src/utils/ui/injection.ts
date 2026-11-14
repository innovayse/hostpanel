/**
 * Provide/inject contracts between kit parents and their parts.
 *
 * A table hands its density to its rows and cells this way, so a screen sets it once on
 * `<UiTable density="compact">` instead of repeating padding classes on every cell.
 */
import type { InjectionKey, Ref } from 'vue'
import type { RowSelection } from '@/composables/useRowSelection'
import type { UiAlign, UiCardRole, UiHideBelow } from '@/utils/ui/table'

/** Closed set of table paddings. */
export const UiTableDensity = {
  /** No padding of its own; the screen's classes decide (compatibility). */
  Plain: 'plain',
  Compact: 'compact',
  Comfortable: 'comfortable',
} as const

/** One of the table paddings. */
export type UiTableDensity = (typeof UiTableDensity)[keyof typeof UiTableDensity]

/** Shape of one table column as its header declares it; the loading skeleton draws its cells from it. */
export interface UiTableColumn {
  /** Horizontal alignment of the column. */
  align?: UiAlign
  /** The column holds numbers or money. */
  numeric?: boolean
  /** The column is the row actions column. */
  actions?: boolean
  /** Breakpoint below which the column is hidden. */
  hideBelow?: UiHideBelow
  /** The column is the checkbox column of a selectable table. */
  select?: boolean
  /** Caption of the column (the text of its header), the label of its value on a card. */
  label?: string
  /** What the column is on a card (a field when omitted). */
  cardRole?: UiCardRole
}

/** What `UiTable` shares with its parts. */
export interface UiTableContext {
  /** Padding applied to header and data cells. */
  density: UiTableDensity
  /** True while the table draws placeholder rows: the body and foot groups render nothing then. */
  loading?: boolean
  /**
   * Registers a header column, in render order, and returns the function that removes it. The getter is read on
   * every render so the column follows its header's props. Absent outside a table.
   */
  registerColumn?: (column: () => UiTableColumn) => () => void
  /** The registered column at a position, which is where a cell finds its caption and role for the card layout. Absent outside a table. */
  columnAt?: (index: number) => UiTableColumn | undefined
  /** The selection of a selectable table; its rows and header draw the checkbox column. Absent on a plain table. */
  selection?: RowSelection
  /** Accessible name of the select-all checkbox in the header. */
  selectAllLabel?: string
  /** Accessible name of a row's checkbox. */
  selectRowLabel?: string
  /** `data-testid` of the select-all checkbox in the header. */
  selectAllTestid?: string
}

/** Injection key for the table context. */
export const UI_TABLE_CONTEXT: InjectionKey<UiTableContext> = Symbol('ui-table-context')

/** Injection key `UiTableHead` provides (true) so a row inside it draws the select-all header cell and not a row checkbox. */
export const UI_TABLE_HEAD: InjectionKey<boolean> = Symbol('ui-table-head')

/**
 * Injection key for the loading flag of a form: a screen that is still reading the stored entry
 * provides it, and every `UiField` below draws a skeleton in place of its control, so the form
 * keeps its real sections and fields while the data arrives.
 */
export const UI_FIELD_LOADING: InjectionKey<Readonly<Ref<boolean>>> = Symbol('ui-field-loading')
