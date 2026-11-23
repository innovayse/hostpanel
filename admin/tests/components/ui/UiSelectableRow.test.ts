/**
 * Unit tests for UiSelectableRow: a chosen row marks itself, the choosing button reports a press, and the trailing
 * control sits outside that button.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSelectableRow from '@/components/ui/UiSelectableRow.vue'

/**
 * Mounts a row with a content and a trailing control.
 *
 * @param selected - Whether the row is the chosen one.
 * @returns The wrapper.
 */
const mountRow = (selected: boolean) => mount(UiSelectableRow, {
  props: { selected, selectTestid: 'choose' },
  attrs: { 'data-testid': 'row' },
  slots: { default: 'Job', trailing: '<i data-testid="extra" />' },
})

describe('UiSelectableRow', () => {
  it('draws the content in a button that says whether it is chosen', () => {
    const wrapper = mountRow(true)
    const button = wrapper.get('[data-testid="choose"]')

    expect(button.text()).toBe('Job')
    expect(button.attributes('aria-pressed')).toBe('true')
    expect(wrapper.attributes('data-selected')).toBe('true')
    expect(wrapper.classes()).toContain('bg-surface-active')
  })

  it('is idle and lights up on hover when not chosen', () => {
    const wrapper = mountRow(false)

    expect(wrapper.get('[data-testid="choose"]').attributes('aria-pressed')).toBe('false')
    expect(wrapper.attributes('data-selected')).toBeUndefined()
    expect(wrapper.classes()).toContain('hover:bg-surface-hover')
  })

  it('emits select when the button is pressed', async () => {
    const wrapper = mountRow(false)

    await wrapper.get('[data-testid="choose"]').trigger('click')

    expect(wrapper.emitted('select')).toHaveLength(1)
  })

  it('keeps the trailing control outside the choosing button', () => {
    const wrapper = mountRow(false)

    expect(wrapper.get('[data-testid="choose"]').find('[data-testid="extra"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="extra"]').exists()).toBe(true)
  })
})
