/**
 * Tests for ReportGroupRow: a heading row spanning the table.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportGroupRow from '@/components/reports/ReportGroupRow.vue'

describe('ReportGroupRow', () => {
  it('shows the group name in one cell that spans the columns', () => {
    const wrapper = mount(ReportGroupRow, { props: { label: 'Hosting', colspan: 5 } })

    expect(wrapper.text()).toBe('Hosting')
    expect(wrapper.get('[data-testid="report-group-label"]').attributes('colspan')).toBe('5')
  })
})
