/**
 * Unit tests for UiSkeletonOf: the skeleton of every screen kind drawn from its schema, with real page text and bars for values.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import UiSkeletonOf from '@/components/ui/UiSkeletonOf.vue'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan, SkeletonTabsOrientation } from '@/utils/ui/skeletonSchema'
import type { CompositeSkeletonSchema, DashboardSkeletonSchema, DetailSkeletonSchema, FormSkeletonSchema, ListSkeletonSchema, ReportSkeletonSchema, SkeletonSchema, TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../../helpers/i18n'
import { countsOf, mountSkeleton, renderedCounts } from '../../helpers/schemaParity'

/** A form of two columns: three sections, six fields of three kinds. */
const form: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: { title: 'shared.clients', subtitle: 'shared.notes', actions: 2 },
  columns: [
    [
      { key: 'identity', title: 'shared.name', fields: [{ key: 'name', label: 'shared.name', required: true }, { key: 'email', label: 'shared.email' }] },
      { key: 'contact', fields: [{ key: 'phone', label: 'shared.phoneNumber', span: SkeletonSpan.Full }] },
    ],
    [
      { key: 'status', title: 'shared.status', fields: [{ key: 'notes', label: 'shared.notes', kind: SkeletonFieldKind.Textarea }, { key: 'active', label: 'shared.status', kind: SkeletonFieldKind.Toggle }, { key: 'agree', kind: SkeletonFieldKind.Check }] },
    ],
  ],
}

/** A list with a search box, one filter, one action, a stat tile and a paged table of three columns. */
const list: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'shared.clients' },
  toolbar: { search: true, filters: ['shared.status'], actions: 1 },
  stats: [{ key: 'total', label: 'shared.clients' }],
  table: { columns: [{ key: 'id', label: 'shared.id' }, { key: 'name', label: 'shared.name' }, { key: 'status', label: 'shared.status', align: 'right' }], rows: 3, pagination: true },
}

/** A detail screen with a title made of data: a card of rows and a card with a table. */
const detail: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { subtitleFromData: true, actions: 1 },
  columns: [
    [{ key: 'profile', title: 'shared.clients', rows: ['shared.name', 'shared.email'] }],
    [{ key: 'history', title: 'shared.status', table: { columns: [{ key: 'id', label: 'shared.id' }, { key: 'status', label: 'shared.status' }] } }],
  ],
}

/** A tab strip layout with a header card of two tiles and three tabs. */
const tabs: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  header: { subtitleFromData: true },
  stats: [{ key: 'a', label: 'shared.clients' }, { key: 'b', label: 'shared.status' }],
  tabs: [{ key: 'one', label: 'shared.name' }, { key: 'two', label: 'shared.email' }, { key: 'three', label: 'shared.company' }],
}

/** A report with two filters, a tile, a chart and a table. */
const report: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'shared.clients', subtitle: 'shared.notes' },
  filters: [{ key: 'from', label: 'shared.name' }, { key: 'to', label: 'shared.email' }],
  stats: [{ key: 'sum', label: 'shared.clients' }],
  chart: { kind: ChartKind.Donut, height: UiChartHeight.Lg },
  table: { columns: [{ key: 'name', label: 'shared.name' }, { key: 'status', label: 'shared.status' }] },
}

/** A dashboard with two tiles and two rows of panels: one card of rows beside one with a table, then one card alone. */
const dashboard: DashboardSkeletonSchema = {
  kind: SkeletonKind.Dashboard,
  header: { title: 'shared.clients' },
  stats: [{ key: 'a', label: 'shared.clients' }, { key: 'b', label: 'shared.status' }],
  panels: [
    {
      kind: SkeletonPartKind.Cards,
      ratio: UiSplitRatio.WideMain,
      columns: [[{ key: 'recent', title: 'shared.name', rows: ['shared.email'] }], [{ key: 'table', title: 'shared.status', table: { columns: [{ key: 'id', label: 'shared.id' }] } }]],
    },
    { kind: SkeletonPartKind.Cards, columns: [[{ key: 'alone', title: 'shared.clients' }]] },
  ],
}

/** Every kind, named. */
const schemas: ReadonlyArray<readonly [string, SkeletonSchema]> = [
  ['form', form],
  ['list', list],
  ['detail', detail],
  ['tabs', tabs],
  ['report', report],
  ['dashboard', dashboard],
]

describe('UiSkeletonOf', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('announces itself as a busy status with the loading text for assistive technology', () => {
    const wrapper = mountSkeleton(form)

    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.attributes('data-testid')).toBe('ui-skeleton-of')
    expect(wrapper.get('.sr-only').text()).toBe('Loading')
  })

  describe.each(schemas)('the %s kind', (_name, schema) => {
    it('draws exactly the sections, fields, columns, tiles and tabs of its schema', () => {
      expect(renderedCounts(mountSkeleton(schema))).toEqual(countsOf(schema))
    })
  })

  describe('the header', () => {
    it('draws the real title and subtitle and a button shaped bar per action', () => {
      const wrapper = mountSkeleton(form)

      expect(wrapper.get('h1').text()).toBe('Clients')
      expect(wrapper.get('header').text()).toContain('Notes')
      expect(wrapper.findAll('header .h-10.w-28')).toHaveLength(2)
    })

    it('folds the actions on a phone like the real header: the first stays, the others hide and one bar stands for the menu', () => {
      const wrapper = mountSkeleton({ ...form, header: { title: 'shared.clients', actions: 3 } })
      const bars = wrapper.findAll('header .h-10.w-28')

      expect(bars).toHaveLength(3)
      expect(bars[0]?.classes()).not.toContain('max-sm:hidden')
      expect(bars.slice(1).every((bar) => bar.classes().includes('max-sm:hidden'))).toBe(true)
      expect(wrapper.find('header [data-testid="ui-skeleton-actions-more"]').exists()).toBe(true)
    })

    it('draws a title bar when the title is made of data, and a subtitle bar when the subtitle is', () => {
      const wrapper = mountSkeleton(detail)

      expect(wrapper.find('h1').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-page-header-skeleton"]').exists()).toBe(true)
      expect(wrapper.findAll('header [data-testid="ui-skeleton-bar"]')).toHaveLength(3)
    })

    it('draws the real title and a bar for a subtitle made of data', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Form, header: { title: 'shared.clients', subtitleFromData: true }, columns: form.columns })

      expect(wrapper.get('h1').text()).toBe('Clients')
      expect(wrapper.find('[data-testid="ui-page-header-subtitle-skeleton"]').exists()).toBe(true)
    })

    it('draws no header for a layout whose header is its own card', () => {
      expect(mountSkeleton({ kind: SkeletonKind.Tabs, tabs: tabs.tabs }).find('header').exists()).toBe(false)
    })
  })

  describe('a form', () => {
    it('keeps the captions and swaps every control for a skeleton', () => {
      const wrapper = mountSkeleton(form)

      expect(wrapper.text()).toContain('Phone Number')
      expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(3)
      expect(wrapper.findAll('[data-testid="ui-form-field-bar"]')).toHaveLength(3)
    })
  })

  describe('a list', () => {
    it('draws the toolbar with its search box, filters button and action bar', () => {
      const wrapper = mountSkeleton(list)

      expect(wrapper.find('input[type="search"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(true)
      expect(wrapper.findAll('.h-10.w-28')).toHaveLength(1)
    })

    it('folds the toolbar actions on a phone like the header ones', () => {
      const wrapper = mountSkeleton({ ...list, toolbar: { search: true, actions: 2 } })

      expect(wrapper.findAll('.h-10.w-28')).toHaveLength(2)
      expect(wrapper.find('[data-testid="ui-skeleton-actions-more"]').exists()).toBe(true)
    })

    it('draws no search box and no filters button for a toolbar that has neither', () => {
      const wrapper = mountSkeleton({ ...list, toolbar: { actions: 2 } })

      expect(wrapper.find('input[type="search"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(false)
    })

    it('draws the real column captions over the schema rows, and a pager', () => {
      const wrapper = mountSkeleton(list)

      expect(wrapper.findAll('[role="columnheader"]').map((header) => header.text())).toEqual(['ID', 'Name', 'Status'])
      expect(wrapper.findAll('[data-testid="ui-table-skeleton"] [role="row"]')).toHaveLength(3)
      expect(wrapper.find('[data-testid="pagination-range"]').exists()).toBe(true)
    })

    it('draws no pager when the schema has none, and no toolbar when it has none', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.List, table: { columns: [{ key: 'id', label: 'shared.id' }] } })

      expect(wrapper.find('[data-testid="pagination-range"]').exists()).toBe(false)
      expect(wrapper.find('input[type="search"]').exists()).toBe(false)
    })
  })

  describe('a detail screen', () => {
    it('draws the card titles, the row captions and a bar per row', () => {
      const wrapper = mountSkeleton(detail)

      expect(wrapper.findAll('[data-testid^="ui-detail-card-"]')).toHaveLength(2)
      expect(wrapper.text()).toContain('Email')
      expect(wrapper.findAll('[data-testid="ui-detail-row"]')).toHaveLength(2)
    })
  })

  describe('a tab strip', () => {
    it('draws the real tab captions in a strip that cannot be used, over the outlet', () => {
      const wrapper = mount(UiSkeletonOf, {
        props: { schema: tabs },
        slots: { outlet: '<p data-testid="child">Child skeleton</p>' },
        global: { plugins: [createTestI18n()] },
      })

      expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['Name', 'Email', 'Company'])
      expect(wrapper.get('[role="tablist"]').element.closest('[inert]')).not.toBeNull()
      expect(wrapper.get('[data-testid="ui-tabs-panel"] [data-testid="child"]').text()).toBe('Child skeleton')
    })

    it('draws the key figures as plain captions with bars in a header card above the strip, as many columns as the width of the row holds', () => {
      const wrapper = mountSkeleton({ ...tabs, actions: 1 })
      const card = wrapper.get('[data-testid="ui-skeleton-header-card"]')

      expect(card.findAll('[data-testid^="ui-stat-row-"]')).toHaveLength(2)
      expect(card.get('[data-testid="ui-stat-row"]').classes()).toContain('grid-cols-[repeat(auto-fit,minmax(9rem,1fr))]')
      expect(card.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(0)
      expect(card.text()).toContain('Clients')
      // The one action of the schema is a button shaped bar beside the identity bars.
      expect(card.findAll('.h-9.w-28')).toHaveLength(1)
    })

    it('folds the actions of the header card on a phone, and keeps the key figures as wide as the row lets them', () => {
      const wrapper = mountSkeleton({ ...tabs, actions: 3 })
      const card = wrapper.get('[data-testid="ui-skeleton-header-card"]')

      expect(card.findAll('.h-9.w-28')).toHaveLength(3)
      expect(card.find('[data-testid="ui-skeleton-actions-more"]').exists()).toBe(true)
      expect(card.get('[data-testid="ui-stat-row"]').classes()).toContain('grid-cols-[repeat(auto-fit,minmax(9rem,1fr))]')
    })

    it('draws a rail of icon placeholders beside the outlet when the tabs are vertical', () => {
      const wrapper = mount(UiSkeletonOf, {
        props: { schema: { kind: SkeletonKind.Tabs, orientation: SkeletonTabsOrientation.Vertical, tabs: tabs.tabs } },
        slots: { outlet: '<p data-testid="child">Child skeleton</p>' },
        global: { plugins: [createTestI18n()] },
      })

      expect(wrapper.findAll('[data-testid="ui-sidebar-skeleton-entry"]').map((entry) => entry.text())).toEqual(['Name', 'Email', 'Company'])
      expect(wrapper.find('[role="tab"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="child"]').text()).toBe('Child skeleton')
      expect(wrapper.attributes('role')).toBe('status')
    })

    it('swaps the rail for a tab strip below md when the schema asks for it', () => {
      const wrapper = mount(UiSkeletonOf, {
        props: { schema: { kind: SkeletonKind.Tabs, orientation: SkeletonTabsOrientation.Vertical, stripBelowMd: true, tabs: tabs.tabs } },
        slots: { outlet: '<p data-testid="child">Child skeleton</p>' },
        global: { plugins: [createTestI18n()] },
      })
      const strip = wrapper.get('[data-testid="ui-skeleton-tab-strip"]')

      expect(strip.classes()).toContain('md:hidden')
      expect(strip.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['Name', 'Email', 'Company'])
      expect(wrapper.get('[data-testid="ui-sidebar-skeleton"]').element.parentElement?.classList.contains('hidden')).toBe(true)
      expect(wrapper.find('[data-testid="child"]').exists()).toBe(true)
    })

    it('keeps the rail on every width when the schema does not ask for a strip', () => {
      const wrapper = mount(UiSkeletonOf, {
        props: { schema: { kind: SkeletonKind.Tabs, orientation: SkeletonTabsOrientation.Vertical, tabs: tabs.tabs } },
        global: { plugins: [createTestI18n()] },
      })

      expect(wrapper.find('[data-testid="ui-skeleton-tab-strip"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="ui-sidebar-skeleton"]').element.parentElement?.classList.contains('hidden')).toBe(false)
    })
  })

  describe('a composite screen', () => {
    it('draws its parts in the order the schema lists them', () => {
      const schema: CompositeSkeletonSchema = {
        kind: SkeletonKind.Composite,
        header: { title: 'shared.clients', backLabel: 'shared.name' },
        parts: [
          { kind: SkeletonPartKind.Toolbar, toolbar: { search: true } },
          { kind: SkeletonPartKind.Form, columns: [[{ key: 'first', fields: [{ key: 'a', label: 'shared.name' }] }]] },
          { kind: SkeletonPartKind.Table, table: { columns: [{ key: 'id', label: 'shared.id' }] } },
          { kind: SkeletonPartKind.Form, columns: [[{ key: 'second', fields: [{ key: 'b', label: 'shared.email' }] }]] },
        ],
      }
      const wrapper = mountSkeleton(schema)
      const html = wrapper.html()

      expect(renderedCounts(wrapper)).toEqual(countsOf(schema))
      expect(html.indexOf('ui-form-section-first')).toBeLessThan(html.indexOf('ui-table-skeleton'))
      expect(html.indexOf('ui-table-skeleton')).toBeLessThan(html.indexOf('ui-form-section-second'))
    })

    it('draws a grid of cards for a tiles part, as many as the schema counts', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Composite, parts: [{ kind: SkeletonPartKind.Tiles, count: 3, facts: 4 }] })

      expect(wrapper.get('[data-testid="ui-skeleton-tiles"]').element.children).toHaveLength(3)
      expect(wrapper.get('[data-testid="ui-skeleton-tiles"]').element.children[0]?.querySelectorAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThan(4 * 2)
    })

    it('draws a back pill with the real caption and no target when the header names one', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Composite, header: { title: 'shared.clients', backLabel: 'shared.name' }, parts: [] })

      expect(wrapper.get('[data-testid="ui-page-header-back"]').text()).toBe('Name')
      expect(wrapper.get('[data-testid="ui-page-header-back"]').attributes('href')).toBeUndefined()
    })
  })

  describe('a report', () => {
    it('draws the filter row, the tiles, a chart box of the schema height and the table', () => {
      const wrapper = mountSkeleton(report)

      expect(wrapper.find('[data-testid="ui-form-section-filters"]').exists()).toBe(true)
      expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(1)
      expect(wrapper.get('[data-testid="chart-skeleton"]').classes()).toContain('h-80')
      expect(wrapper.get('[data-testid="chart-skeleton"]').classes()).toContain('max-sm:h-80')
      expect(wrapper.get('[data-testid="chart-skeleton"]').attributes('data-kind')).toBe(ChartKind.Donut)
      expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    })

    it('draws only the parts the report has', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Report, header: { title: 'shared.clients' }, stats: report.stats })

      expect(wrapper.find('[data-testid="ui-form-section-filters"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="chart-skeleton"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(false)
    })

    it('uses the medium chart height when the schema names none', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Report, chart: { kind: ChartKind.Line } })

      expect(wrapper.get('[data-testid="chart-skeleton"]').classes()).toContain('h-64')
    })

    it('draws a placeholder for each part the data fills in, where the schema holds its place', () => {
      const wrapper = mountSkeleton({
        kind: SkeletonKind.Report,
        map: true,
        alert: true,
        summary: true,
        chart: { kind: ChartKind.Line },
        table: { columns: [{ key: 'name', label: 'shared.name' }] },
        closing: true,
        note: ReportNote.Converted,
      })

      expect(wrapper.find('[data-testid="ui-skeleton-map"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="ui-skeleton-alert"]').exists()).toBe(true)
      expect(wrapper.findAll('[data-testid="ui-skeleton-lines"]')).toHaveLength(2)
      expect(wrapper.find('[data-testid="ui-skeleton-note"]').exists()).toBe(true)
    })

    it('draws none of them for a schema without those parts', () => {
      const wrapper = mountSkeleton(report)

      expect(wrapper.find('[data-testid="ui-skeleton-map"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-skeleton-alert"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-skeleton-lines"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="ui-skeleton-note"]').exists()).toBe(false)
    })

    it('draws a button shaped bar for each button of the schema plus each control of its own in the header', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Report, header: { title: 'shared.clients', actions: 1 }, actions: [ReportAction.Refresh, ReportAction.Print] })

      expect(wrapper.findAll('.h-10.w-28')).toHaveLength(3)
    })

    it('opens the placeholder rows of a grouped table with a group heading placeholder', () => {
      const wrapper = mountSkeleton({ kind: SkeletonKind.Report, table: { columns: [{ key: 'name', label: 'shared.name' }], grouped: true } })

      expect(wrapper.find('[data-testid="ui-table-skeleton-group"]').exists()).toBe(true)
    })
  })

  describe('the dashboard', () => {
    it('draws the tiles and the cards of every row of panels', () => {
      const wrapper = mountSkeleton(dashboard)

      expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(2)
      expect(wrapper.findAll('[data-testid^="ui-detail-card-"]')).toHaveLength(3)
    })

    it('lays each row out with its own width ratio', () => {
      const html = mountSkeleton(dashboard).html()

      expect(html).toContain('lg:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)]')
      expect(html.indexOf('ui-detail-card-alone')).toBeGreaterThan(html.indexOf('ui-detail-card-table'))
    })

    it('draws the placeholder of a panel chart in the shape of its kind', () => {
      const wrapper = mountSkeleton({
        ...dashboard,
        panels: [{ kind: SkeletonPartKind.Cards, columns: [[{ key: 'trend', title: 'shared.name', chart: { kind: ChartKind.Area, height: UiChartHeight.Short } }]] }],
      })

      const chart = wrapper.get('[data-testid="chart-skeleton"]')
      expect(chart.attributes('data-kind')).toBe(ChartKind.Area)
      expect(chart.classes()).toContain('h-52')
    })
  })
})
