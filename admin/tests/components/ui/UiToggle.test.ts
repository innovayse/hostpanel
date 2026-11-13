/**
 * Unit tests for UiToggle: switch semantics, flipping and the disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiToggle from '@/components/ui/UiToggle.vue'

describe('UiToggle', () => {
  it('is a switch that reports its state', () => {
    const on = mount(UiToggle, { props: { modelValue: true } })
    const off = mount(UiToggle, { props: { modelValue: false } })

    expect(on.attributes('role')).toBe('switch')
    expect(on.attributes('aria-checked')).toBe('true')
    expect(off.attributes('aria-checked')).toBe('false')
  })

  it('emits the flipped state on click', async () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('does nothing while disabled', async () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false, disabled: true } })

    await wrapper.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.attributes('disabled')).toBeDefined()
  })

  it('keeps data-testid on the button', () => {
    const wrapper = mount(UiToggle, { props: { modelValue: false }, attrs: { 'data-testid': 'probe' } })

    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('colours the track from the state', () => {
    expect(mount(UiToggle, { props: { modelValue: true } }).classes()).toContain('not-disabled:gradient-brand')
    expect(mount(UiToggle, { props: { modelValue: false } }).classes()).toContain('bg-control-track')
  })

  it('shows a focus ring and a muted disabled track', () => {
    const wrapper = mount(UiToggle, { props: { modelValue: true, disabled: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['focus-visible:ring-2', 'disabled:bg-surface-hover', 'disabled:cursor-not-allowed']))
  })
})
