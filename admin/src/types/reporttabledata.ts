/**
 * The data of a report's table: what the report page needs, beside the columns of its schema, to draw it.
 */
import type { ReportColumn, ReportColumnExtra } from '@/types/reportcolumn'

/** The pager under a paged report table. */
export interface ReportPagination {
  /** The page shown, starting at 1. */
  page: number
  /** How many rows match the filters, over every page. */
  total: number
  /** Rows on a page. */
  pageSize: number
  /** Prefix of the `data-testid` of the pager. */
  testid: string
}

/**
 * The rows and the cell formats of a report's table.
 *
 * @typeParam T - The row type.
 */
export interface ReportTableData<T extends object> {
  /** Rows to draw; none until the report is read, or for a table that draws its own body. */
  rows?: T[]
  /** What identifies a row: the name of a unique property, or a function building a key from the row. Required whenever `rows` is given. */
  rowKey?: (keyof T & string) | ((row: T) => string)
  /** How the cells of the schema columns are written, by column key; the captions, alignment and width are the schema's. */
  extras?: Readonly<Record<string, ReportColumnExtra>>
  /** Columns that replace a schema column known only from the data, by its key (one amount column per currency). */
  expansions?: Readonly<Record<string, readonly ReportColumn[]>>
  /** Footer figures by column key. */
  totals?: Record<string, string | number> | null
  /** Caption of the footer row; "Total" when omitted. */
  totalsLabel?: string
  /** Formats a money cell; US dollars when omitted. */
  formatMoney?: (amount: number) => string
  /** Forces the empty state on or off; by default it follows `rows`. */
  empty?: boolean
  /** The pager under the table; none when omitted. */
  pagination?: ReportPagination
}
