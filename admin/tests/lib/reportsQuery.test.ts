/**
 * Tests for the pure helpers of the query reports.
 */
import { describe, expect, it } from 'vitest'
import { buildQueryParams, filterPage, QUERY_REPORT_DEFAULT_PRESET, reportPath } from '@/lib/reportsQuery'
import { DateRangePreset, matchDateRangePreset, presetIsoRange } from '@/utils/ui/datePicker'
import type { ReportQueryConfig, ReportQueryRange } from '@/types/reportquery'

/** A report with a server-side status filter and a paged endpoint. */
const config: ReportQueryConfig = { path: '/reports/invoices', serverFields: ['status'], pageSize: 25, defaultColumns: ['id'], defaultRanges: {}, defaultFilterField: 'status' }

/** Two ranges, both sent to the endpoint. */
const ranges: ReportQueryRange[] = [
  { key: 'created', label: 'Created', param: 'created' },
  { key: 'due', label: 'Due', param: 'due' },
]

describe('buildQueryParams', () => {
  it('sends server filters, the chosen ranges, and the page', () => {
    const query = buildQueryParams(
      config,
      ranges,
      {
        filters: [
          { field: 'status', condition: 'exact', value: 'Paid' },
          { field: 'clientName', condition: 'contains', value: 'a' },
        ],
        ranges: { created: ['2024-01-01', '2024-12-31'], due: ['2024-02-01', '2024-02-02'] },
        page: 3,
      },
      true,
    )

    expect(query).toBe('status=Paid&createdFrom=2024-01-01&createdTo=2024-12-31&dueFrom=2024-02-01&dueTo=2024-02-02&page=3&pageSize=25')
  })

  it('leaves the page out for the export', () => {
    const query = buildQueryParams(config, ranges, { filters: [], ranges: { created: null }, page: 2 }, false)

    expect(query).toBe('')
  })
})

describe('filterPage', () => {
  const rows = [
    { clientName: 'Acme Ltd', status: 'Paid' },
    { clientName: 'Beta', status: 'Unpaid' },
  ]

  it('keeps rows that contain the text, ignoring case', () => {
    expect(filterPage(rows, [{ field: 'clientName', condition: 'contains', value: 'ACME' }], [])).toEqual([rows[0]])
  })

  it('keeps rows that match exactly', () => {
    expect(filterPage(rows, [{ field: 'clientName', condition: 'exact', value: 'beta' }], [])).toEqual([rows[1]])
  })

  it('skips filters on fields the endpoint already applied', () => {
    expect(filterPage(rows, [{ field: 'status', condition: 'exact', value: 'Paid' }], ['status'])).toEqual(rows)
  })

  it('reads a nested field by its dot path', () => {
    const nested = [{ address: { city: 'Yerevan' } }, { address: { city: 'Gyumri' } }]

    expect(filterPage(nested, [{ field: 'address.city', condition: 'exact', value: 'gyumri' }], [])).toEqual([nested[1]])
  })
})

describe('reportPath', () => {
  it('appends only the parameters that are set', () => {
    expect(reportPath('/reports/x', { year: 2024, month: null, q: '' })).toBe('/reports/x?year=2024')
  })

  it('returns the bare path when nothing is set', () => {
    expect(reportPath('/reports/x')).toBe('/reports/x')
  })
})

describe('QUERY_REPORT_DEFAULT_PRESET', () => {
  it('is the current month, and the range it gives is the one the picker lights up', () => {
    expect(QUERY_REPORT_DEFAULT_PRESET).toBe(DateRangePreset.ThisMonth)
    expect(matchDateRangePreset(presetIsoRange(QUERY_REPORT_DEFAULT_PRESET))).toBe(QUERY_REPORT_DEFAULT_PRESET)
  })
})
