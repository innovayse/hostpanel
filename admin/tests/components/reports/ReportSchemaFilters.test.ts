/**
 * Tests for ReportSchemaFilters: one captioned field per filter of the schema, each holding the control the screen
 * puts in its `field-<key>` slot, sized by the width the screen names, and the Generate action; the month, year and currency
 * dropdowns it draws itself when no slot fills them, in the order and under the caption of the schema.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportSchemaFilters from '@/components/reports/ReportSchemaFilters.vue'
import { ReportFieldWidth } from '@/utils/ui/reportField'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../../helpers/i18n'

/** A report schema with two filters. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  filters: [{ key: 'year', label: 'shared.year' }, { key: 'dateRange', label: 'shared.dateRange' }],
}

/**
 * Mounts the filters with a control in each field slot.
 *
 * @param props - Props on top of the schema.
 * @returns The wrapper.
 */
const mountFilters = (props: Record<string, unknown> = {}) =>
  mount(ReportSchemaFilters, {
    props: { schema, ...props },
    slots: {
      'field-year': '<span data-testid="control-year">year</span>',
      'field-dateRange': '<span data-testid="control-range">range</span>',
    },
    global: { plugins: [createTestI18n()] },
  })

/** A schema with the month before the year, as the month reports have it, and the currency. */
const periodSchema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  filters: [
    { key: 'month', label: 'shared.month' },
    { key: 'year', label: 'shared.year' },
    { key: 'currency', label: 'shared.currency' },
  ],
}

describe('ReportSchemaFilters', () => {
  it('draws one field per filter of the schema, captioned with its text, in order', () => {
    const wrapper = mountFilters()

    expect(wrapper.findAll('label').map(label => label.text())).toEqual(['Year', 'Date Range'])
  })

  it('puts the control of each field slot in its field', () => {
    const wrapper = mountFilters()

    expect(wrapper.get('[data-testid="control-year"]').text()).toBe('year')
    expect(wrapper.get('[data-testid="control-range"]').text()).toBe('range')
  })

  it('sizes a field by the width the screen names and gives the year the narrow one', () => {
    const wrapper = mountFilters({ widths: { dateRange: ReportFieldWidth.Grow } })
    const fields = wrapper.findAll('label').map(label => label.element.parentElement?.className ?? '')

    expect(fields[0]).toContain('w-28')
    expect(fields[1]).toContain('flex-1')
  })

  it('draws no field for a schema without filters', () => {
    const wrapper = mountFilters({ schema: { kind: SkeletonKind.Report } })

    expect(wrapper.findAll('label')).toHaveLength(0)
  })

  it('asks for the report from the Generate action, labelled as the screen says', async () => {
    const wrapper = mountFilters({ generateLabel: 'Run' })

    await wrapper.get('[data-testid="report-generate"]').trigger('click')

    expect(wrapper.get('[data-testid="report-generate"]').text()).toBe('Run')
    expect(wrapper.emitted('generate')).toHaveLength(1)
  })
  describe('period dropdowns', () => {
    /**
     * Mounts the filters of a period schema, with no slot filling them.
     *
     * @param filterSchema - The schema.
     * @param props - Props on top of the schema.
     * @returns The wrapper.
     */
    const mountPeriod = (filterSchema: ReportSkeletonSchema, props: Record<string, unknown> = {}) =>
      mount(ReportSchemaFilters, { props: { schema: filterSchema, year: 2024, ...props }, global: { plugins: [createTestI18n()] } })

    it('draws only the dropdowns the schema names', () => {
      const wrapper = mountPeriod(schema)

      expect(wrapper.find('[data-testid="report-year"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="report-month"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-currency"]').exists()).toBe(false)
    })

    it('draws the month, the year and the currency in the order of the schema, under its captions', () => {
      const wrapper = mountPeriod(periodSchema)
      const order = ['report-month', 'report-year', 'report-currency'].map(id => wrapper.html().indexOf(`data-testid="${id}"`))

      expect(order.every(position => position >= 0)).toBe(true)
      expect([...order].sort((a, b) => a - b)).toEqual(order)
      expect(wrapper.findAll('label').map(label => label.text())).toEqual(['Month', 'Year', 'Currency'])
    })

    it('shows the chosen year, makes its field the narrow one and offers the years asked for', () => {
      const wrapper = mountPeriod({ kind: SkeletonKind.Report, filters: [{ key: 'year', label: 'shared.year' }] }, { yearsBack: 5, yearsAhead: 5 })

      expect(wrapper.get('[data-testid="report-year"]').text()).toContain('2024')
      expect(wrapper.get('label').element.parentElement?.className).toContain('w-28')
    })

    it('leaves a slot of the screen in charge of the field', () => {
      const wrapper = mount(ReportSchemaFilters, {
        props: { schema: periodSchema, year: 2024 },
        slots: { 'field-year': '<span data-testid="own-year">own</span>' },
        global: { plugins: [createTestI18n()] },
      })

      expect(wrapper.find('[data-testid="own-year"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="report-year"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="report-month"]').exists()).toBe(true)
    })
  })
})
