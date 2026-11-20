/**
 * Shared checks of the report screens that are drawn from a skeleton schema: the captions the schema names are on
 * the screen, and a screen whose report is still loading keeps every block of its schema (filters, tiles, chart,
 * table with its columns) instead of dropping the ones that need data.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Component } from 'vue'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from './i18n'
import { mountView } from './mount'

/** The English catalogue, which turns the locale keys of a schema into the texts a reader sees. */
const i18n = createTestI18n()

/** One screen to check against its schema. */
export interface ReportSchemaCase {
  /** Screen name, for the test title. */
  name: string
  /** The screen. */
  view: Component
  /** The schema the screen is drawn from. */
  schema: ReportSkeletonSchema
}

/**
 * Lists the texts a report schema puts on its screen: the title, the line under it, the filter captions, the tile
 * captions and the column captions, in English.
 *
 * @param schema - The report schema.
 * @returns The texts, without the ones the schema leaves out.
 */
export const schemaCaptionsOf = (schema: ReportSkeletonSchema): string[] => {
  const keys = [
    schema.header?.title,
    schema.header?.subtitle,
    ...(schema.filters ?? []).map(field => field.label),
    ...(schema.stats ?? []).map(stat => stat.label),
    ...(schema.table?.columns ?? []).map(column => column.label),
  ]
  return keys.flatMap(key => (key === undefined ? [] : [i18n.global.t(key)]))
}

/**
 * Registers the loading-state checks of a list of report screens: every request stays in flight, so the screen is
 * drawn as it is while its report loads.
 *
 * @param cases - The screens with their schemas.
 */
export const runReportSchemaCases = (cases: ReportSchemaCase[]): void => {
  describe('report screens drawn from their schema while loading', () => {
    afterEach(() => {
      vi.unstubAllGlobals()
    })

    it.each(cases)('$name shows the captions and every block of its schema', async ({ view, schema }) => {
      vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
      const wrapper = await mountView(view)

      for (const caption of schemaCaptionsOf(schema)) {
        expect(wrapper.text()).toContain(caption)
      }
      expect(wrapper.findAll('[data-testid^="report-stat-"]:not([data-testid$="-tooltip"])')).toHaveLength(schema.stats?.length ?? 0)
      expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(schema.chart !== undefined)
      expect(wrapper.find('[data-testid="report-filters"]').exists()).toBe(schema.filters !== undefined)
      expect(wrapper.find('[data-testid="report-table"]').exists()).toBe(schema.table !== undefined)
      expect(wrapper.findAll('[role="columnheader"]')).toHaveLength(schema.table?.columns.length ?? 0)
    })
  })
}
