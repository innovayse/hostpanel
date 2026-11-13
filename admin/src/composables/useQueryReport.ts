/**
 * State and actions of a filterable query report (clients, domains, invoices, services,
 * transactions).
 *
 * The screen declares what the report is (`ReportQueryConfig`); this composable owns the filters,
 * the chosen columns, the page, and the calls to the reports store, once for all five query
 * screens.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import type { Ref } from 'vue'
import type { FilterRow } from '@/components/ui/UiAdvancedFilters.vue'
import { buildQueryParams, filterPage } from '@/lib/reportsQuery'
import { useReportsStore } from '@/stores/reports'
import type { ReportColumn } from '@/types/reportcolumn'
import type { PaginatedList } from '@/types/paginatedlist'
import type { ReportQueryConfig, ReportQueryRange, ReportRangeValue } from '@/types/reportquery'

/**
 * Creates the state of one query report and loads its first page.
 *
 * @param config - What the report is.
 * @param view - The columns and date ranges the screen shows, as refs so a language switch applies.
 * @param view.columns - Columns the screen offers.
 * @param view.ranges - Date ranges the screen offers.
 * @returns A reactive object with the rows, the filter state and the actions the screen binds.
 */
export const useQueryReport = <T extends { id: number }>(
  config: ReportQueryConfig,
  view: { columns: Readonly<Ref<readonly ReportColumn[]>>; ranges: Readonly<Ref<readonly ReportQueryRange[]>> },
) => {
  const store = useReportsStore()

  /** Rows of the current page that match the local filters. */
  const rows = ref([]) as Ref<T[]>

  /** Number of rows over all pages, as the endpoint counts them. */
  const totalCount = ref(0)

  /** Current page, starting at 1. */
  const page = ref(1)

  /** Complete advanced-filter rows. */
  const filters = ref<FilterRow[]>([])

  /** Chosen date range per range key; nothing chosen at first. */
  const ranges = ref<Record<string, ReportRangeValue>>(
    Object.fromEntries(view.ranges.value.map(range => [range.key, null])),
  )

  /** Keys of the columns the operator wants to see; none until chosen. */
  const visibleKeys = ref<Set<string>>(new Set())

  /** Columns to draw, in the report's own order. */
  const activeColumns = computed<ReportColumn[]>(() => view.columns.value.filter(column => visibleKeys.value.has(column.key)))

  /** Loads the current page. */
  const load = async (): Promise<void> => {
    const params = buildQueryParams(config, view.ranges.value, { filters: filters.value, ranges: ranges.value, page: page.value }, true)
    const result = await store.fetchReport<PaginatedList<T>>(`${config.path}?${params}`)
    if (result === null) {
      return
    }
    rows.value = filterPage(result.items, filters.value, config.serverFields)
    totalCount.value = result.totalCount
  }

  /** Starts over from the first page with the chosen filters. */
  const generate = async (): Promise<void> => {
    page.value = 1
    await load()
  }

  /**
   * Moves to another page.
   *
   * @param next - Page number to show.
   */
  const goToPage = async (next: number): Promise<void> => {
    page.value = next
    await load()
  }

  /**
   * Shows or hides one column.
   *
   * @param key - Column key.
   */
  const toggleColumn = (key: string): void => {
    const next = new Set(visibleKeys.value)
    if (next.has(key)) {
      next.delete(key)
    } else {
      next.add(key)
    }
    visibleKeys.value = next
  }

  /**
   * Replaces the advanced filters.
   *
   * @param next - The complete filter rows.
   */
  const setFilters = (next: FilterRow[]): void => {
    filters.value = next
  }

  /**
   * Sets one date range.
   *
   * @param key - Range key.
   * @param value - The chosen range, or null to clear it.
   */
  const setRange = (key: string, value: ReportRangeValue): void => {
    ranges.value = { ...ranges.value, [key]: value }
  }

  /** Shows every column. */
  const selectAllColumns = (): void => {
    visibleKeys.value = new Set(view.columns.value.map(column => column.key))
  }

  /** Hides every column. */
  const clearColumns = (): void => {
    visibleKeys.value = new Set()
  }

  /** Opens the browser print dialog for the report. */
  const print = (): void => {
    window.print()
  }

  /** Opens the CSV export of the chosen filters in a new tab. */
  const exportCsv = (): void => {
    if (!config.exportPath) {
      return
    }
    const params = buildQueryParams(config, view.ranges.value, { filters: filters.value, ranges: ranges.value, page: page.value }, false)
    window.open(store.exportUrl(params === '' ? config.exportPath : `${config.exportPath}?${params}`), '_blank')
  }

  onMounted(load)

  // Reactive, so a screen can hand the whole state to a component and read plain values from it.
  return reactive({
    rows,
    totalCount,
    page,
    pageSize: config.pageSize,
    filters,
    ranges,
    visibleKeys,
    activeColumns,
    load,
    generate,
    goToPage,
    setFilters,
    setRange,
    toggleColumn,
    selectAllColumns,
    clearColumns,
    print,
    exportCsv,
  })
}
