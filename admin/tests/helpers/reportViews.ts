/**
 * Shared runner of the report screen smoke tests: each case mounts one screen with its endpoints
 * answered at the network level and checks it shows its title and data, the refusal sentence
 * when the API refuses, and, for a screen that offers Retry, that a retry after a refusal shows the
 * data. A case that names its skeleton schema is also held to it: the screen draws the schema's header, filters,
 * tiles, chart and table columns while the report loads and again once it is read, so the skeleton and the screen
 * cannot differ.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import type { Component } from 'vue'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { json, problem } from './fetch'
import { createTestI18n } from './i18n'
import { mountView } from './mount'
import { stubRouteTable } from './routes'
import type { RouteTable } from './routes'

/** One screen to smoke-test. */
export interface ReportCase {
  /** Screen name, for the test title. */
  name: string
  /** The screen. */
  view: Component
  /** Answers by `"GET /reports/<name>"`; the client picker is always answered. */
  routes: RouteTable
  /** Texts the mounted screen must contain (its title, then something from its data). */
  expected: string[]
  /** True when a refused load offers Retry, which loads the report again. */
  retryable?: boolean
  /** The screen's skeleton schema; the screen must draw its blocks while loading and once read. */
  schema?: ReportSkeletonSchema
  /** True when the table's columns depend on the data (one per currency), so only the loading state is held to the schema's columns. */
  dataColumns?: boolean
  /** True when the answer has no rows, so the table gives way to its empty state once the report is read. */
  emptyResult?: boolean
}

/** Answer of an endpoint whose request never finishes: the screen stays in its loading state. */
const pending = (): Promise<Response> => new Promise<Response>(() => undefined)

/** A case that names its schema. */
type SchemaCase = ReportCase & { schema: ReportSkeletonSchema }

/**
 * Picks the cases that name a schema.
 *
 * @param cases - All the cases.
 * @returns The cases held to their schema.
 */
const withSchema = (cases: ReportCase[]): SchemaCase[] =>
  cases.flatMap(reportCase => (reportCase.schema === undefined ? [] : [{ ...reportCase, schema: reportCase.schema }]))

/**
 * Asserts that a mounted report screen draws the blocks of its schema.
 *
 * @param wrapper - The mounted screen.
 * @param schema - The schema it must follow.
 * @param options - What to hold the screen to.
 * @param options.title - Whether the schema's title must show (a screen that names the month in it has the schema's only before it is read).
 * @param options.table - Whether the table must be drawn (an answer with no rows shows an empty state in its place).
 * @param options.columns - Whether the table's header captions must be the schema's.
 */
export const expectSchemaBlocks = (wrapper: VueWrapper, schema: ReportSkeletonSchema, options: { title: boolean; table: boolean; columns: boolean }): void => {
  const { t } = createTestI18n().global
  const text = wrapper.text()
  if (options.title && schema.header?.title !== undefined) {
    expect(text).toContain(t(schema.header.title))
  }
  if (schema.header?.subtitle !== undefined) {
    expect(text).toContain(t(schema.header.subtitle))
  }
  expect(wrapper.findAll('[data-testid="report-refresh"], [data-testid="report-export"], [data-testid="report-print"]')).toHaveLength(schema.actions?.length ?? 0)
  for (const filter of schema.filters ?? []) {
    if (filter.label !== undefined) {
      expect(text).toContain(t(filter.label))
    }
  }
  expect(wrapper.findAll('[data-testid^="report-stat-"]:not([data-testid$="-tooltip"])')).toHaveLength(schema.stats?.length ?? 0)
  expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(schema.chart !== undefined)
  if (options.table) {
    expect(wrapper.find('table').exists()).toBe(schema.table !== undefined)
  }
  if (options.columns && schema.table !== undefined) {
    const captions = wrapper.findAll('table')[0]?.findAll('th').map(header => header.text())
    expect(captions).toEqual(schema.table.columns.map(column => (column.label === undefined ? '' : t(column.label))))
  }
}

/**
 * Registers the smoke tests of a list of screens.
 *
 * @param cases - The screens.
 */
export const runReportCases = (cases: ReportCase[]): void => {
  describe('report screens', () => {
    afterEach(() => {
      vi.unstubAllGlobals()
    })

    it.each(cases)('$name shows its title and its data', async (reportCase) => {
      stubRouteTable({ 'GET /reports/client-picker': () => json([{ id: 1, name: 'Acme' }]), ...reportCase.routes })
      const wrapper = await mountView(reportCase.view)

      for (const text of reportCase.expected) {
        expect(wrapper.text()).toContain(text)
      }
      expect(wrapper.find('[data-testid="report-error"]').exists()).toBe(false)
    })

    it.each(withSchema(cases))(
      '$name draws every block of its schema while the report loads',
      async (reportCase) => {
        const unanswered = Object.fromEntries(Object.keys(reportCase.routes).map(route => [route, pending]))
        stubRouteTable({ 'GET /reports/client-picker': () => json([]), ...unanswered })
        const wrapper = await mountView(reportCase.view)

        expectSchemaBlocks(wrapper, reportCase.schema, { title: true, table: true, columns: true })
      },
    )

    it.each(withSchema(cases))(
      '$name draws the same blocks of its schema once the report is read',
      async (reportCase) => {
        stubRouteTable({ 'GET /reports/client-picker': () => json([{ id: 1, name: 'Acme' }]), ...reportCase.routes })
        const wrapper = await mountView(reportCase.view)

        expectSchemaBlocks(wrapper, reportCase.schema, {
          title: false,
          table: reportCase.emptyResult !== true,
          columns: reportCase.dataColumns !== true && reportCase.emptyResult !== true,
        })
      },
    )

    it.each(cases.filter(reportCase => Object.keys(reportCase.routes).length > 0))(
      '$name shows the sentence the API refused with',
      async (reportCase) => {
        const refused = Object.fromEntries(Object.keys(reportCase.routes).map(route => [route, () => problem('No access.', 403)]))
        stubRouteTable({ 'GET /reports/client-picker': () => json([]), ...refused })
        const wrapper = await mountView(reportCase.view)

        expect(wrapper.get('[data-testid="report-error"]').text()).toBe('No access.')
      },
    )

    it.each(cases.filter(reportCase => reportCase.retryable === true))(
      '$name loads the report again from Retry after a refusal',
      async (reportCase) => {
        /** Routes already refused once; each answers normally from its second call. */
        const refusedOnce = new Set<string>()
        const flaky = Object.fromEntries(Object.entries(reportCase.routes).map(([route, answer]) => [
          route,
          (body: unknown) => {
            if (refusedOnce.has(route)) {
              return answer(body)
            }
            refusedOnce.add(route)
            return problem('No access.', 403)
          },
        ]))
        stubRouteTable({ 'GET /reports/client-picker': () => json([]), ...flaky })
        const wrapper = await mountView(reportCase.view)

        await wrapper.get('[data-testid="report-error-retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.find('[data-testid="report-error"]').exists()).toBe(false)
        for (const text of reportCase.expected) {
          expect(wrapper.text()).toContain(text)
        }
      },
    )
  })
}

/**
 * Registers the smoke tests of one screen.
 *
 * @param reportCase - The screen.
 */
export const runReportCase = (reportCase: ReportCase): void => {
  runReportCases([reportCase])
}
