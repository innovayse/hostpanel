/**
 * Pure helpers of the filterable query reports: turning the filter state into a query string and
 * narrowing a returned page by the filters the endpoint does not apply itself.
 */
import type { FilterRow } from '@/components/ui/UiAdvancedFilters.vue'
import { buildQuery } from '@/composables/useApi'
import type { QueryValue } from '@/composables/useApi'
import type { ReportQueryConfig, ReportQueryRange, ReportRangeValue } from '@/types/reportquery'
import { valueAtPath } from '@/lib/valueAtPath'
import { DateRangePreset } from '@/utils/ui/datePicker'
import { UiFilterCondition } from '@/utils/ui/filter'

/** The preset every date range of a query report starts on, so the five reports open on the same period and the picker lights it up. */
export const QUERY_REPORT_DEFAULT_PRESET = DateRangePreset.ThisMonth

/** What the operator has chosen on a query report. */
export interface ReportQueryState {
  /** Complete advanced-filter rows. */
  filters: FilterRow[]
  /** Chosen date range per range key. */
  ranges: Record<string, ReportRangeValue>
  /** Page number, starting at 1. */
  page: number
}

/**
 * Builds the query string of a report request.
 *
 * Equality filters on the endpoint's own fields and the chosen date ranges are sent; the page
 * and its size are added unless `paged` is false (the export takes everything).
 *
 * @param config - What the report is.
 * @param ranges - The date-range filters of the report.
 * @param state - What the operator has chosen.
 * @param paged - Whether to add `page` and `pageSize`.
 * @returns The query string without a leading `?`.
 */
export const buildQueryParams = (
  config: ReportQueryConfig,
  ranges: readonly ReportQueryRange[],
  state: ReportQueryState,
  paged: boolean,
): string => {
  const params = new URLSearchParams()
  for (const field of config.serverFields) {
    const filter = state.filters.find(f => f.field === field)
    if (filter) {
      params.set(field, filter.value)
    }
  }
  for (const range of ranges) {
    const value = state.ranges[range.key]
    if (value) {
      params.set(`${range.param}From`, value[0])
      params.set(`${range.param}To`, value[1])
    }
  }
  if (paged) {
    params.set('page', String(state.page))
    params.set('pageSize', String(config.pageSize))
  }
  return params.toString()
}

/**
 * Narrows a returned page by the filters the endpoint did not apply.
 *
 * @param items - The rows of the page.
 * @param filters - Complete advanced-filter rows.
 * @param serverFields - Fields the endpoint already applied; their filters are skipped here.
 * @returns The rows that match every remaining filter, exactly or by containing the text.
 */
export const filterPage = <T extends object>(items: T[], filters: FilterRow[], serverFields: readonly string[]): T[] =>
  items.filter(row => {
    for (const filter of filters) {
      if (serverFields.includes(filter.field)) {
        continue
      }
      const cell = String(valueAtPath(row, filter.field) ?? '').toLowerCase()
      const wanted = filter.value.toLowerCase()
      if (filter.condition === UiFilterCondition.Exact && cell !== wanted) {
        return false
      }
      if (filter.condition === UiFilterCondition.Contains && !cell.includes(wanted)) {
        return false
      }
    }
    return true
  })

/**
 * Builds a report request path from an endpoint and its parameters.
 *
 * Empty, null and undefined parameters are left out, so an unset filter adds nothing.
 *
 * @param path - Endpoint path, e.g. `/reports/annual-income`.
 * @param params - Parameter name to value.
 * @returns The path with its query string, or the bare path when no parameter is set.
 */
export const reportPath = (path: string, params: Record<string, QueryValue> = {}): string => `${path}${buildQuery(params)}`
