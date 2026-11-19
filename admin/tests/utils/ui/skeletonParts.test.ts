/**
 * Tests of the blocks a screen schema is made of: a report lists its parts in the order they are drawn, and only the
 * ones its schema has.
 */
import { describe, expect, it } from 'vitest'
import { ChartKind } from '@/utils/ui/chart'
import { ReportNote } from '@/utils/ui/reportParts'
import { partsOf } from '@/utils/ui/skeletonParts'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** A report with every part. */
const full: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  filters: [{ key: 'year', label: 'shared.year' }],
  stats: [{ key: 'total', label: 'shared.total' }],
  map: true,
  alert: true,
  summary: true,
  chart: { kind: ChartKind.Line },
  table: { columns: [{ key: 'name', label: 'shared.name' }] },
  closing: true,
  note: ReportNote.Converted,
}

describe('partsOf', () => {
  it('lists the parts of a report in the order they are drawn', () => {
    expect(partsOf(full).map(part => part.kind)).toEqual([
      SkeletonPartKind.Form,
      SkeletonPartKind.Stats,
      SkeletonPartKind.Map,
      SkeletonPartKind.Alert,
      SkeletonPartKind.Lines,
      SkeletonPartKind.Chart,
      SkeletonPartKind.Table,
      SkeletonPartKind.Lines,
      SkeletonPartKind.Note,
    ])
  })

  it('lists only the parts a report has', () => {
    expect(partsOf({ kind: SkeletonKind.Report, table: full.table }).map(part => part.kind)).toEqual([SkeletonPartKind.Table])
    expect(partsOf({ kind: SkeletonKind.Report }).map(part => part.kind)).toEqual([])
  })
})
