/**
 * Unit tests for UiFieldSelector: count on the trigger, checkbox list and the three actions.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFieldSelector from '@/components/ui/UiFieldSelector.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Fields offered to the selector. */
const fields = [
  { key: 'a', label: 'Alpha' },
  { key: 'b', label: 'Beta' },
] as const

/**
 * Mounts the selector with the English catalogue.
 *
 * @param selected - Keys that are selected.
 * @returns The wrapper.
 */
const mountSelector = (selected: string[]) =>
  mount(UiFieldSelector, { props: { fields, selected: new Set(selected) }, global: { plugins: [createTestI18n()] } })

describe('UiFieldSelector', () => {
  it('shows how many fields are selected', () => {
    expect(mountSelector(['a']).get('[data-testid="field-selector-trigger"]').text()).toContain('1/2')
  })

  it('hides the count when nothing is selected', () => {
    expect(mountSelector([]).get('[data-testid="field-selector-trigger"]').text()).not.toContain('/')
  })

  it('opens the list with a checkbox per field reflecting the selection', async () => {
    const wrapper = mountSelector(['a'])

    await wrapper.get('[data-testid="field-selector-trigger"]').trigger('click')

    expect(wrapper.get('[data-testid="field-selector-field-a"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="field-selector-field-b"]').attributes('aria-checked')).toBe('false')
  })

  it('reports toggles, select all and clear all', async () => {
    const wrapper = mountSelector([])
    await wrapper.get('[data-testid="field-selector-trigger"]').trigger('click')

    await wrapper.get('[data-testid="field-selector-field-b"]').trigger('click')
    await wrapper.get('[data-testid="field-selector-select-all"]').trigger('click')
    await wrapper.get('[data-testid="field-selector-clear-all"]').trigger('click')

    expect(wrapper.emitted('toggle')?.[0]).toEqual(['b'])
    expect(wrapper.emitted('selectAll')).toHaveLength(1)
    expect(wrapper.emitted('clearAll')).toHaveLength(1)
  })

  it('closes on apply and on a click outside', async () => {
    const wrapper = mountSelector([])
    await wrapper.get('[data-testid="field-selector-trigger"]').trigger('click')

    await wrapper.get('[data-testid="field-selector-apply"]').trigger('click')
    expect(wrapper.find('[data-testid="field-selector-apply"]').exists()).toBe(false)

    await wrapper.get('[data-testid="field-selector-trigger"]').trigger('click')
    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="field-selector-apply"]').exists()).toBe(false)
  })

  it('names the panel it opens and closes on Escape with the focus back on the trigger', async () => {
    const wrapper = mount(UiFieldSelector, { props: { fields, selected: new Set<string>() }, attachTo: document.body, global: { plugins: [createTestI18n()] } })
    const trigger = wrapper.get('[data-testid="field-selector-trigger"]')
    expect(trigger.attributes('aria-expanded')).toBe('false')

    await trigger.trigger('click')
    expect(trigger.attributes('aria-controls')).toBe(wrapper.get('[role="group"]').attributes('id'))

    await trigger.trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('[role="group"]').exists()).toBe(false)
    expect(document.activeElement).toBe(trigger.element)
    wrapper.unmount()
  })
})
