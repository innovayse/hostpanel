/**
 * Tests for ReportFilterBar: the slots and the Generate and Clear actions.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the bar.
 *
 * @param props - Props to switch actions on.
 * @returns The wrapper.
 */
const mountBar = (props: Record<string, unknown> = {}) =>
  mount(ReportFilterBar, {
    props,
    slots: { fields: '<span data-testid="field-slot">field</span>', default: '<span data-testid="extra-slot">extra</span>' },
    global: { plugins: [createTestI18n()] },
  })

describe('ReportFilterBar', () => {
  it('renders both slots and only the Generate action by default', () => {
    const wrapper = mountBar()

    expect(wrapper.find('[data-testid="field-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="extra-slot"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="report-generate"]').text()).toBe('Generate')
    expect(wrapper.find('[data-testid="report-clear"]').exists()).toBe(false)
  })

  it('uses the given caption for the main button', () => {
    expect(mountBar({ generateLabel: 'Apply' }).get('[data-testid="report-generate"]').text()).toBe('Apply')
  })

  it('emits each action when its button is pressed', async () => {
    const wrapper = mountBar({ clearable: true })

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await wrapper.get('[data-testid="report-clear"]').trigger('click')

    expect(wrapper.emitted('generate')).toHaveLength(1)
    expect(wrapper.emitted('clear')).toHaveLength(1)
  })
})
