/**
 * Tests for ReportStatRow: one tile per stat, with its label, value and hint.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import ReportStatRow from '@/components/reports/ReportStatRow.vue'
import { REPORT_LOADING } from '@/lib/reportLoading'
import type { ReportStat } from '@/types/reportstat'

/** Two tiles, the second with a hint. */
const stats: ReportStat[] = [
  { key: 'total', label: 'Total income', value: '$120.00' },
  { key: 'best', label: 'Best month', value: 'March', hint: '$80.00' },
]

describe('ReportStatRow', () => {
  it('draws a tile per stat with its label, value and hint', () => {
    const wrapper = mount(ReportStatRow, { props: { stats } })

    expect(wrapper.findAll('[data-testid^="report-stat-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="report-stat-total"]').text()).toContain('Total income')
    expect(wrapper.get('[data-testid="report-stat-total"]').text()).toContain('$120.00')
    expect(wrapper.get('[data-testid="report-stat-best"]').text()).toContain('$80.00')
  })

  it('draws skeleton tiles while the report loads', () => {
    const wrapper = mount(ReportStatRow, { props: { stats }, global: { provide: { [REPORT_LOADING as symbol]: ref(true) } } })

    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(2)
  })

  it('draws no tile for an empty list', () => {
    const wrapper = mount(ReportStatRow, { props: { stats: [] } })

    expect(wrapper.findAll('[data-testid^="report-stat-"]')).toHaveLength(0)
  })

  it('explains a metric in a tooltip on an info icon, only for a stat that has one', () => {
    const wrapper = mount(ReportStatRow, {
      props: { stats: [{ key: 'net', label: 'Net', value: '$1', tooltip: 'Income minus refunds' }, { key: 'gross', label: 'Gross', value: '$2' }] },
    })

    expect(wrapper.get('[data-testid="report-stat-net-tooltip"]').attributes('aria-label')).toBe('Income minus refunds')
    expect(wrapper.get('[role="tooltip"]').text()).toBe('Income minus refunds')
    expect(wrapper.find('[data-testid="report-stat-gross-tooltip"]').exists()).toBe(false)
  })
})
