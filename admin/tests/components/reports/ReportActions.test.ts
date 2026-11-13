/**
 * Tests for ReportActions: each button shows only when switched on and emits when pressed.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportActions from '@/components/reports/ReportActions.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the actions.
 *
 * @param props - Props to switch buttons on.
 * @returns The wrapper.
 */
const mountActions = (props: Record<string, unknown> = {}) =>
  mount(ReportActions, { props, global: { plugins: [createTestI18n()] } })

describe('ReportActions', () => {
  it('shows no button by default', () => {
    const wrapper = mountActions()

    expect(wrapper.find('[data-testid="report-export"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-print"]').exists()).toBe(false)
  })

  it('emits export and print when their buttons are pressed', async () => {
    const wrapper = mountActions({ exportable: true, printable: true })

    await wrapper.get('[data-testid="report-export"]').trigger('click')
    await wrapper.get('[data-testid="report-print"]').trigger('click')

    expect(wrapper.emitted('export')).toHaveLength(1)
    expect(wrapper.emitted('print')).toHaveLength(1)
  })
})
