/**
 * Tests of the report schema readers: the headline, the table columns (with a placeholder column replaced by columns
 * known from the data), the stat tiles, the date ranges of a query report, a filter caption, the filter fields and the default column keys.
 */
import { describe, expect, it } from 'vitest'
import {
  REPORT_FIELDS_KEY,
  reportColumn,
  reportColumnKeys,
  reportColumns,
  reportFilterFields,
  reportFilterLabel,
  reportHeadline,
  reportQueryRanges,
  reportStats,
} from '@/lib/reportsSchema'
import { ReportCellFormat } from '@/types/reportcolumn'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/**
 * Translates a key the way the tests read it: the key itself, upper-cased, so a caption is told from its key.
 *
 * @param key - The locale key.
 * @returns The translated text.
 */
const t = (key: string): string => key.toUpperCase()

/** A report schema with every block. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'a.title', subtitle: 'a.subtitle' },
  filters: [
    { key: REPORT_FIELDS_KEY, label: 'a.fields' },
    { key: 'created', label: 'a.created' },
    { key: 'noCaption' },
  ],
  stats: [{ key: 'total', label: 'a.total' }, { key: 'best', label: 'a.best' }],
  table: {
    columns: [
      { key: 'month', label: 'a.month' },
      { key: 'amount', label: 'a.amount', align: UiAlign.Right },
    ],
  },
}

describe('reportHeadline', () => {
  it('translates the title and the description', () => {
    expect(reportHeadline(schema, t)).toEqual({ title: 'A.TITLE', description: 'A.SUBTITLE' })
  })

  it('gives an empty title and no description when the schema has no header', () => {
    expect(reportHeadline({ kind: SkeletonKind.Report }, t)).toEqual({ title: '', description: undefined })
  })
})

describe('reportColumns', () => {
  it('translates the captions and keeps the alignment of the schema', () => {
    expect(reportColumns(schema.table, t)).toEqual([
      { key: 'month', label: 'A.MONTH', align: undefined },
      { key: 'amount', label: 'A.AMOUNT', align: UiAlign.Right },
    ])
  })

  it('keeps the width the schema gives a column', () => {
    const sized = { key: 'name', label: 'a.name', size: UiColumnSize.Grow }

    expect(reportColumn(sized, t).size).toBe(UiColumnSize.Grow)
    expect(reportColumns({ columns: [sized] }, t)[0]?.size).toBe(UiColumnSize.Grow)
    expect(reportColumns(schema.table, t)[0]?.size).toBeUndefined()
  })

  it('adds the cell format the screen gives a column', () => {
    const [, amount] = reportColumns(schema.table, t, { amount: { format: ReportCellFormat.Money } })

    expect(amount?.format).toBe(ReportCellFormat.Money)
  })

  it('replaces a placeholder column with the columns known from the data, and keeps it while there are none', () => {
    const perCurrency = [reportColumn({ key: 'USD', label: 'a.usd' }, t), reportColumn({ key: 'EUR', label: 'a.eur' }, t)]

    expect(reportColumns(schema.table, t, {}, { amount: perCurrency }).map(column => column.key)).toEqual(['month', 'USD', 'EUR'])
    expect(reportColumns(schema.table, t, {}, { amount: [] }).map(column => column.key)).toEqual(['month', 'amount'])
  })

  it('gives no column for a schema without a table', () => {
    expect(reportColumns(undefined, t)).toEqual([])
  })
})

describe('reportStats', () => {
  it('translates the captions and fills the figures by key, an empty one for a tile with none', () => {
    expect(reportStats(schema.stats, t, { total: { value: '$10' } })).toEqual([
      { key: 'total', label: 'A.TOTAL', value: '$10' },
      { key: 'best', label: 'A.BEST', value: '' },
    ])
  })

  it('carries the hint and the tooltip of a figure', () => {
    expect(reportStats(schema.stats, t, { total: { value: 1, hint: 'h', tooltip: 'explains' } })[0]).toEqual({
      key: 'total',
      label: 'A.TOTAL',
      value: 1,
      hint: 'h',
      tooltip: 'explains',
    })
  })
})

describe('reportQueryRanges', () => {
  it('turns every filter but the column picker into a date range sending its own key', () => {
    expect(reportQueryRanges(schema.filters, t)).toEqual([
      { key: 'created', label: 'A.CREATED', param: 'created' },
      { key: 'noCaption', label: '', param: 'noCaption' },
    ])
  })
})

describe('reportFilterLabel', () => {
  it('translates the caption of a filter by its key', () => {
    expect(reportFilterLabel(schema, 'created', t)).toBe('A.CREATED')
  })

  it('is empty for an unknown filter and for a filter with no caption', () => {
    expect(reportFilterLabel(schema, 'missing', t)).toBe('')
    expect(reportFilterLabel(schema, 'noCaption', t)).toBe('')
  })
})

describe('reportColumnKeys', () => {
  it('lists the keys of the table columns in order', () => {
    expect(reportColumnKeys(schema.table)).toEqual(['month', 'amount'])
  })

  it('is empty without a table', () => {
    expect(reportColumnKeys(undefined)).toEqual([])
  })
})

describe('reportFilterFields', () => {
  it('keeps the filterable columns in column order, captioned like the column', () => {
    const columns = [
      { key: 'id', label: 'Id' },
      { key: 'status', label: 'Status' },
      { key: 'email', label: 'Email' },
    ]
    expect(reportFilterFields(columns, ['email', 'status'])).toEqual([
      { value: 'status', label: 'Status' },
      { value: 'email', label: 'Email' },
    ])
  })

  it('is empty when no column is filterable', () => {
    expect(reportFilterFields([{ key: 'id', label: 'Id' }], [])).toEqual([])
  })
})
