/**
 * Tests for ReportStatusBadge: the status word, toned by its meaning.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportStatusBadge from '@/components/reports/ReportStatusBadge.vue'

describe('ReportStatusBadge', () => {
  it('shows the word with the success tone for a good status', () => {
    const wrapper = mount(ReportStatusBadge, { props: { status: 'Active' } })

    expect(wrapper.text()).toBe('Active')
    expect(wrapper.classes().join(' ')).toContain('text-status-green')
  })

  it('words a compound status word, not the raw identifier', () => {
    expect(mount(ReportStatusBadge, { props: { status: 'PartiallyRefunded' } }).text()).toBe('Partially refunded')
  })

  it('shows a bad status in the danger tone', () => {
    expect(mount(ReportStatusBadge, { props: { status: 'Overdue' } }).classes().join(' ')).toContain('text-status-red')
  })
})
