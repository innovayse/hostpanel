/**
 * Tests for useQueryReport: first page on mount, paging, filters (sent and local), range
 * parameters, column choice, print and export.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h, ref } from 'vue'
import { useQueryReport } from '@/composables/useQueryReport'
import type { ReportColumn } from '@/types/reportcolumn'
import { DateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import type { ReportQueryConfig, ReportQueryRange } from '@/types/reportquery'
import { json } from '../helpers/fetch'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** One row of the test report. */
interface Row {
  /** Row id. */
  id: number
  /** Client name. */
  clientName: string
  /** Status word. */
  status: string
}

/** The test report: status is filtered by the endpoint, the export has a path. */
const config: ReportQueryConfig = {
  path: '/reports/things',
  serverFields: ['status'],
  exportPath: '/reports/things/export',
  pageSize: 2,
  defaultColumns: ['id'],
  defaultRanges: {},
  defaultFilterField: 'status',
}

/** Every column of the test report. */
const columns = ref<ReportColumn[]>([
  { key: 'id', label: 'ID' },
  { key: 'clientName', label: 'Client' },
])

/** The one date range of the test report. */
const ranges = ref<ReportQueryRange[]>([{ key: 'created', label: 'Created', param: 'created' }])

/**
 * Mounts a host around the composable and hands back its state.
 *
 * @param settings - What the report is; the plain test report by default.
 * @returns The wrapper and the report state.
 */
const mountHost = (settings: ReportQueryConfig = config) => {
  let state!: ReturnType<typeof useQueryReport<Row>>
  const wrapper = mount(
    defineComponent({
      setup() {
        state = useQueryReport<Row>(settings, { columns, ranges })
        return () => h('p', state.rows.map(row => row.clientName).join(','))
      },
    }),
    { global: { plugins: [createPinia()] } },
  )
  return { wrapper, state }
}

describe('useQueryReport', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('loads the first page on mount', async () => {
    const mock = stubRouteTable({
      'GET /reports/things': () => json({ items: [{ id: 1, clientName: 'Acme', status: 'Paid' }], pageNumber: 1, pageSize: 50, totalCount: 7, totalPages: 1 }),
    })
    const { wrapper, state } = mountHost()
    await flushPromises()

    expect(requestsOf(mock)).toEqual(['GET /reports/things?page=1&pageSize=2'])
    expect(wrapper.text()).toBe('Acme')
    expect(state.totalCount).toBe(7)
  })

  it('moves to another page and starts over from page 1 on generate', async () => {
    const mock = stubRouteTable({ 'GET /reports/things': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const { state } = mountHost()
    await flushPromises()

    await state.goToPage(3)
    await state.generate()

    expect(requestsOf(mock).slice(1)).toEqual([
      'GET /reports/things?page=3&pageSize=2',
      'GET /reports/things?page=1&pageSize=2',
    ])
  })

  it('sends the filters the endpoint applies and narrows the page by the others', async () => {
    const mock = stubRouteTable({
      'GET /reports/things': () => json({
        items: [{ id: 1, clientName: 'Acme', status: 'Paid' }, { id: 2, clientName: 'Beta', status: 'Paid' }],
        pageNumber: 1,
        pageSize: 50,
        totalCount: 2,
        totalPages: 1,
      }),
    })
    const { wrapper, state } = mountHost()
    await flushPromises()

    state.setFilters([
      { field: 'status', condition: 'exact', value: 'Paid' },
      { field: 'clientName', condition: 'contains', value: 'bet' },
    ])
    await state.generate()
    await flushPromises()

    expect(requestsOf(mock).at(-1)).toBe('GET /reports/things?status=Paid&page=1&pageSize=2')
    expect(wrapper.text()).toBe('Beta')
  })

  it('sends a chosen date range under its parameter prefix', async () => {
    const mock = stubRouteTable({ 'GET /reports/things': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const { state } = mountHost()
    await flushPromises()

    state.setRange('created', ['2024-01-01', '2024-01-31'])
    await state.generate()

    expect(requestsOf(mock).at(-1)).toBe('GET /reports/things?createdFrom=2024-01-01&createdTo=2024-01-31&page=1&pageSize=2')
  })

  it('starts a range on its default preset and sends it with the first page', async () => {
    const mock = stubRouteTable({ 'GET /reports/things': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const { state } = mountHost({ ...config, defaultRanges: { created: DateRangePreset.Last30Days } })
    await flushPromises()

    const [from, to] = presetIsoRange(DateRangePreset.Last30Days)
    expect(state.ranges).toEqual({ created: [from, to] })
    expect(requestsOf(mock)[0]).toBe(`GET /reports/things?createdFrom=${from}&createdTo=${to}&page=1&pageSize=2`)
  })

  it('starts with the default columns, then toggles, selects and clears them', async () => {
    stubRouteTable({ 'GET /reports/things': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const { state } = mountHost()
    await flushPromises()

    expect(state.activeColumns.map(column => column.key)).toEqual(['id'])
    state.toggleColumn('clientName')
    expect(state.activeColumns.map(column => column.key)).toEqual(['id', 'clientName'])
    state.toggleColumn('id')
    state.toggleColumn('clientName')
    expect(state.activeColumns).toEqual([])
    state.selectAllColumns()
    expect(state.activeColumns).toHaveLength(2)
    state.clearColumns()
    expect(state.activeColumns).toEqual([])
  })

  it('opens the export of the chosen filters without paging, and prints', async () => {
    stubRouteTable({ 'GET /reports/things': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) })
    const open = vi.fn()
    const print = vi.fn()
    vi.stubGlobal('open', open)
    vi.stubGlobal('print', print)
    const { state } = mountHost()
    await flushPromises()

    state.setFilters([{ field: 'status', condition: 'exact', value: 'Paid' }])
    state.exportCsv()
    state.print()

    expect(open).toHaveBeenCalledWith('/api/reports/things/export?status=Paid', '_blank')
    expect(print).toHaveBeenCalledTimes(1)
  })
})
