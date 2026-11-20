/**
 * Tests for ReportActions: the buttons are the list the report schema gives, in that order, and each emits when pressed.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportActions from '@/components/reports/ReportActions.vue'
import { ReportAction } from '@/utils/ui/reportParts'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the actions.
 *
 * @param actions - The buttons the schema lists.
 * @returns The wrapper.
 */
const mountActions = (actions: ReportAction[] = []) =>
  mount(ReportActions, { props: { actions }, global: { plugins: [createTestI18n()] } })

describe('ReportActions', () => {
  it('shows no button for an empty list', () => {
    const wrapper = mountActions()

    expect(wrapper.find('[data-testid="report-refresh"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-export"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-print"]').exists()).toBe(false)
  })

  it('shows only the buttons the list names, in its order', () => {
    const wrapper = mountActions([ReportAction.Print, ReportAction.Refresh])

    expect(wrapper.findAll('button').map(button => button.attributes('data-testid'))).toEqual(['report-print', 'report-refresh'])
  })

  it('draws an icon beside each label and keeps the group on one line', () => {
    const wrapper = mountActions([ReportAction.Export, ReportAction.Print])

    expect(wrapper.find('[data-testid="report-export"] svg').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-print"] svg').exists()).toBe(true)
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['shrink-0', 'whitespace-nowrap']))
    expect(wrapper.classes()).not.toContain('flex-wrap')
  })

  it('emits export and print when their buttons are pressed', async () => {
    const wrapper = mountActions([ReportAction.Export, ReportAction.Print])

    await wrapper.get('[data-testid="report-export"]').trigger('click')
    await wrapper.get('[data-testid="report-print"]').trigger('click')

    expect(wrapper.emitted('export')).toHaveLength(1)
    expect(wrapper.emitted('print')).toHaveLength(1)
  })

  it('shows a refresh button with an icon and emits refresh when pressed', async () => {
    const wrapper = mountActions([ReportAction.Refresh])

    expect(wrapper.find('[data-testid="report-refresh"] svg').exists()).toBe(true)
    await wrapper.get('[data-testid="report-refresh"]').trigger('click')

    expect(wrapper.emitted('refresh')).toHaveLength(1)
  })

  it('draws the controls of the screen from the default slot before the buttons', () => {
    const wrapper = mount(ReportActions, {
      props: { actions: [ReportAction.Print] },
      slots: { default: '<i data-testid="toggle" />' },
      global: { plugins: [createTestI18n()] },
    })
    const order = wrapper.findAll('[data-testid="toggle"], [data-testid="report-print"]').map((node) => node.attributes('data-testid'))

    expect(order).toEqual(['toggle', 'report-print'])
  })
})
