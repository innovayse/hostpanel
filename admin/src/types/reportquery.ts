/**
 * Shapes of the filterable "query" reports (clients, domains, invoices, services, transactions):
 * one endpoint that takes date ranges and equality filters, answers one page, and lets the
 * operator choose which columns to see.
 */
import type { FilterRow } from '@/components/ui/UiAdvancedFilters.vue'
import type { ReportColumn } from '@/types/reportcolumn'

/** A date range as the range picker holds it: from and to, or nothing chosen. */
export type ReportRangeValue = [string, string] | null

/** One date-range filter of a query report. */
export interface ReportQueryRange {
  /** Stable key of the range (also the suffix of its test id). */
  key: string
  /** Caption above the picker (already translated). */
  label: string
  /** Prefix of the two query parameters the range sends (`<param>From`, `<param>To`). */
  param: string
}

/** What a query report needs to know about itself; the captions live in the screen. */
export interface ReportQueryConfig {
  /** Endpoint path without query string, e.g. `/reports/invoices`. */
  path: string
  /**
   * Filter fields the endpoint applies itself (sent as `<field>=<value>`); filters on any other
   * field are applied to the returned page.
   */
  serverFields: readonly string[]
  /** Path of the CSV export, when the report has one. */
  exportPath?: string
  /** Rows per page. */
  pageSize: number
}

/** What the query report page reads from the state a screen builds with `useQueryReport`. */
export interface ReportQueryView {
  /** Rows of the current page. */
  rows: { id: number }[]
  /** Number of rows over all pages. */
  totalCount: number
  /** Current page, starting at 1. */
  page: number
  /** Rows per page. */
  pageSize: number
  /** Complete advanced-filter rows. */
  filters: FilterRow[]
  /** Chosen date range per range key. */
  ranges: Record<string, ReportRangeValue>
  /** Keys of the columns the operator wants to see. */
  visibleKeys: Set<string>
  /** Columns to draw. */
  activeColumns: ReportColumn[]
  /** Loads the current page again (the Retry of a failed load). */
  load: () => Promise<void>
  /** Starts over from the first page with the chosen filters. */
  generate: () => Promise<void>
  /** Moves to another page. */
  goToPage: (next: number) => Promise<void>
  /** Replaces the advanced filters. */
  setFilters: (next: FilterRow[]) => void
  /** Sets one date range. */
  setRange: (key: string, value: ReportRangeValue) => void
  /** Shows or hides one column. */
  toggleColumn: (key: string) => void
  /** Shows every column. */
  selectAllColumns: () => void
  /** Hides every column. */
  clearColumns: () => void
  /** Opens the print dialog. */
  print: () => void
  /** Opens the CSV export. */
  exportCsv: () => void
}
