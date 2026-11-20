/**
 * Tests for ReportEmptyState: the one compact empty state of the report screens, with its icon, its
 * sentence and its hint, and the defaults of both texts.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportEmptyState from '@/components/reports/ReportEmptyState.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the state.
 *
 * @param props - Props to pass.
 * @returns The wrapper.
 */
const mountState = (props: Record<string, unknown> = {}) =>
  mount(ReportEmptyState, { props, global: { plugins: [createTestI18n()] } })

describe('ReportEmptyState', () => {
  it('shows the icon, the default sentence and the hint to change the filters', () => {
    const wrapper = mountState()

    expect(wrapper.find('[data-testid="report-empty"] svg').exists()).toBe(true)
    expect(wrapper.text()).toContain('No data for this report.')
    expect(wrapper.text()).toContain('Try another period or change the filters.')
  })

  it('takes its own sentence, hint and test id', () => {
    const wrapper = mountState({ title: 'No invoices.', hint: 'Pick a year.', testid: 'custom-empty' })

    expect(wrapper.get('[data-testid="custom-empty"]').text()).toContain('No invoices.')
    expect(wrapper.text()).toContain('Pick a year.')
  })

  it('puts the call to action of the actions slot under the sentence', () => {
    const wrapper = mount(ReportEmptyState, { slots: { actions: '<button data-testid="go">Fetch</button>' }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.get('[data-testid="ui-empty-state-actions"] [data-testid="go"]').text()).toBe('Fetch')
  })

  it('draws no action area without the slot', () => {
    expect(mountState().find('[data-testid="ui-empty-state-actions"]').exists()).toBe(false)
  })

  it('uses the compact padding', () => {
    expect(mountState().find('.py-6').exists()).toBe(true)
  })
})
