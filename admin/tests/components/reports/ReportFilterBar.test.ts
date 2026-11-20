/**
 * Tests for ReportFilterBar: the slots and the Generate and Clear actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import ReportFilterBar from '@/components/reports/ReportFilterBar.vue'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

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

describe('ReportFilterBar on a phone', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /**
   * Mounts the bar with the teleport inlined, so the sheet is queryable.
   *
   * @param props - Props of the bar.
   * @returns The wrapper.
   */
  const mountPhoneBar = (props: Record<string, unknown> = {}) =>
    mount(ReportFilterBar, {
      props,
      slots: { fields: '<span data-testid="field-slot">field</span>', default: '<span data-testid="extra-slot">extra</span>' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

  it('folds the card into a Filters button with the count, and keeps the controls out of the page until it is pressed', () => {
    stubPhone(true)
    const wrapper = mountPhoneBar({ activeCount: 3 })

    expect(wrapper.find('[data-testid="report-generate"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="report-filter-trigger"]').text()).toContain('Filters')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
    expect(wrapper.find('[data-testid="field-slot"]').exists()).toBe(false)
  })

  it('opens the fields and the extra rows in the Filters sheet, whose Apply generates and Clear clears', async () => {
    stubPhone(true)
    const wrapper = mountPhoneBar({ clearable: true })

    await wrapper.get('[data-testid="report-filter-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.get('h2').text()).toBe('Filters')
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="field-slot"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="extra-slot"]').exists()).toBe(true)

    await wrapper.get('[data-testid="filter-reset"]').trigger('click')
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
    expect(wrapper.emitted('generate')).toHaveLength(1)
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
  })

  it('has no Clear in the sheet unless the screen is clearable, and keeps the inline card on a wide screen', async () => {
    stubPhone(true)
    const phone = mountPhoneBar()
    await phone.get('[data-testid="report-filter-trigger"]').trigger('click')
    expect(phone.find('[data-testid="filter-reset"]').exists()).toBe(false)

    stubPhone(false)
    const wide = mountPhoneBar()
    expect(wide.find('[data-testid="report-filter-trigger"]').exists()).toBe(false)
    expect(wide.find('[data-testid="report-generate"]').exists()).toBe(true)
  })
})
