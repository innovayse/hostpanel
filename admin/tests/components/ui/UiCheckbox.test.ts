/**
 * Unit tests for UiCheckbox: checkbox semantics, toggling, change event and disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'

describe('UiCheckbox', () => {
  it('is a checkbox that reports its state', () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: true } })

    expect(wrapper.attributes('role')).toBe('checkbox')
    expect(wrapper.attributes('aria-checked')).toBe('true')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('shows no tick when unchecked', () => {
    expect(mount(UiCheckbox, { props: { modelValue: false } }).find('svg').exists()).toBe(false)
  })

  it('emits the flipped state and a change event on click', async () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: false } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
    expect(wrapper.emitted('change')).toHaveLength(1)
  })

  it('hands the click to the change listener, so a caller can read the Shift key', async () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: false } })

    await wrapper.trigger('click', { shiftKey: true })

    const click = wrapper.emitted('change')?.[0]?.[0] as MouseEvent | undefined

    expect(click?.shiftKey).toBe(true)
  })

  it('shows a dash and reports mixed when indeterminate', () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: false, indeterminate: true } })

    expect(wrapper.attributes('aria-checked')).toBe('mixed')
    expect(wrapper.find('line').exists()).toBe(true)
    expect(wrapper.find('polyline').exists()).toBe(false)
    expect(wrapper.classes()).toContain('not-disabled:gradient-brand')
  })

  it('does nothing while disabled', async () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: false, disabled: true } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('keeps data-testid on the button', () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: false }, attrs: { 'data-testid': 'probe' } })

    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('shows a disabled box as muted with a not-allowed cursor', () => {
    const wrapper = mount(UiCheckbox, { props: { modelValue: true, disabled: true } })

    expect(wrapper.attributes('disabled')).toBeDefined()
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['disabled:bg-surface-hover', 'disabled:cursor-not-allowed', 'focus-visible:ring-2']))
  })
})
