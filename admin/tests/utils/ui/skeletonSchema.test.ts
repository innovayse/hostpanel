/**
 * Tests of the skeleton schema: the closed value sets and the discriminated union every screen schema is one of.
 */
import { describe, expect, it } from 'vitest'
import { ChartKind } from '@/utils/ui/chart'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import {
  SkeletonFieldKind,
  SkeletonKind,
  SkeletonPartKind,
  SkeletonSpan,
  SkeletonTabsOrientation,
} from '@/utils/ui/skeletonSchema'
import type {
  ChartReportSkeletonSchema,
  CompositeSkeletonSchema,
  DashboardSkeletonSchema,
  DetailSkeletonSchema,
  FormSkeletonSchema,
  ListSkeletonSchema,
  SkeletonSchema,
  TabsSkeletonSchema,
} from '@/utils/ui/skeletonSchema'

/** A form screen: two columns, the first with one section of an input, a text area and a radio group. */
const form: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: { title: 'clients.new' },
  columns: [
    [
      {
        key: 'main',
        title: 'clients.main',
        columns: 1,
        fields: [
          { key: 'name', label: 'clients.name', required: true },
          { key: 'bio', label: 'clients.bio', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'type', label: 'clients.type', kind: SkeletonFieldKind.Radio, options: ['clients.person', 'clients.company'] },
        ],
      },
    ],
    [],
  ],
}

/** A list screen with a toolbar, stat tiles and a table. */
const list: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  toolbar: { search: true, filters: ['clients.status'], actions: 1 },
  stats: [{ key: 'total', label: 'shared.total' }],
  table: { columns: [{ key: 'name', label: 'clients.name' }], rows: 5, pagination: true },
}

/** A detail screen with one half-width card of rows. */
const detail: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  columns: [[{ key: 'info', title: 'clients.info', rows: ['clients.name', 'clients.email'], span: SkeletonSpan.Half }]],
}

/** A tabs layout with a vertical rail. */
const tabs: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  tabs: [{ key: 'overview', label: 'tabs.overview' }],
  orientation: SkeletonTabsOrientation.Vertical,
  stripBelowMd: true,
}

/** A report with a chart. */
const report: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh, ReportAction.Export],
  chart: { kind: ChartKind.Line },
  note: ReportNote.Recurring,
}

/** A dashboard with one row of panels. */
const dashboard: DashboardSkeletonSchema = {
  kind: SkeletonKind.Dashboard,
  stats: [{ key: 'revenue', label: 'dashboard.revenue' }],
  panels: [{ kind: SkeletonPartKind.Cards, columns: [[{ key: 'recent', title: 'dashboard.recent' }]] }],
}

/** A composite screen holding one part of every kind. */
const composite: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  parts: [
    { kind: SkeletonPartKind.Stats, stats: [{ key: 'a', label: 'shared.a' }], columns: 2 },
    { kind: SkeletonPartKind.Form, columns: form.columns },
    { kind: SkeletonPartKind.Cards, columns: detail.columns },
    { kind: SkeletonPartKind.Toolbar, toolbar: { search: true } },
    { kind: SkeletonPartKind.Table, table: list.table },
    { kind: SkeletonPartKind.Tabs, tabs: tabs.tabs },
    { kind: SkeletonPartKind.Chart, chart: { kind: ChartKind.Line } },
    { kind: SkeletonPartKind.Tiles, count: 3, facts: 2 },
    { kind: SkeletonPartKind.Map },
    { kind: SkeletonPartKind.Lines },
    { kind: SkeletonPartKind.Alert },
    { kind: SkeletonPartKind.Note },
  ],
}

describe('SkeletonKind', () => {
  it('lists the seven screen shapes', () => {
    expect(Object.values(SkeletonKind)).toEqual(['form', 'list', 'detail', 'tabs', 'report', 'composite', 'dashboard'])
  })

  it('has one schema shape per kind, each carrying its own discriminant', () => {
    const schemas: SkeletonSchema[] = [form, list, detail, tabs, report, dashboard, composite]
    expect(schemas.map((schema) => schema.kind).sort()).toEqual(Object.values(SkeletonKind).sort())
  })
})

describe('SkeletonFieldKind', () => {
  it('lists the controls a field can hold', () => {
    expect(Object.values(SkeletonFieldKind)).toEqual(['input', 'textarea', 'toggle', 'check', 'radio'])
  })

  it('is optional on a field, which then is an input', () => {
    const field = form.columns[0]?.[0]?.fields[0]
    expect(field).toBeDefined()
    expect(field?.kind).toBeUndefined()
    expect(field?.required).toBe(true)
  })
})

describe('SkeletonSpan', () => {
  it('has a half and a full row', () => {
    expect(Object.values(SkeletonSpan)).toEqual(['half', 'full'])
    expect(form.columns[0]?.[0]?.fields[1]?.span).toBe(SkeletonSpan.Full)
  })
})

describe('SkeletonTabsOrientation', () => {
  it('has a horizontal strip and a vertical rail', () => {
    expect(Object.values(SkeletonTabsOrientation)).toEqual(['horizontal', 'vertical'])
    expect(tabs.orientation).toBe(SkeletonTabsOrientation.Vertical)
  })
})

describe('SkeletonPartKind', () => {
  it('lists the twelve blocks of a composite screen', () => {
    expect(Object.values(SkeletonPartKind)).toEqual([
      'stats',
      'form',
      'cards',
      'toolbar',
      'table',
      'tabs',
      'chart',
      'tiles',
      'map',
      'lines',
      'alert',
      'note',
    ])
  })

  it('has one part shape per kind in a composite screen', () => {
    expect(composite.parts.map((part) => part.kind)).toEqual(Object.values(SkeletonPartKind))
  })

  it('lets a dashboard panel be a cards part', () => {
    expect(dashboard.panels.every((panel) => panel.kind === SkeletonPartKind.Cards)).toBe(true)
  })
})

describe('report schema', () => {
  it('takes its header buttons and note from the report vocabulary', () => {
    expect(report.actions).toEqual([ReportAction.Refresh, ReportAction.Export])
    expect(report.note).toBe(ReportNote.Recurring)
  })

  it('guarantees a chart on the chart report type', () => {
    expect(report.chart.kind).toBe(ChartKind.Line)
  })
})
