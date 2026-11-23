/**
 * Unit tests for UiToggle: switch semantics, the requested value, loading and the disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiToggle from '~/components/ui/UiToggle.vue'

describe('UiToggle', () => {
  it('is a switch with an accessible name and state', () => {
    const wrapper = mountWithKit(UiToggle, { props: { modelValue: true, label: 'Lock', testid: 'lock' } })
    const toggle = wrapper.find('[data-testid="lock"]')

    expect(toggle.attributes('role')).toBe('switch')
    expect(toggle.attributes('aria-checked')).toBe('true')
    expect(toggle.attributes('aria-label')).toBe('Lock')
  })

  it('widens its touch area to 44px with a pseudo-element', () => {
    const wrapper = mountWithKit(UiToggle, { props: { modelValue: false, label: 'Lock' } })

    expect(wrapper.find('button').classes()).toEqual(expect.arrayContaining(['before:absolute', 'before:-inset-y-2.5']))
  })

  it('asks for the opposite value and does not flip itself', async () => {
    const wrapper = mountWithKit(UiToggle, { props: { modelValue: false, label: 'Lock' } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
    expect(wrapper.find('button').attributes('aria-checked')).toBe('false')
  })

  it('shows a spinner and ignores clicks while loading', async () => {
    const wrapper = mountWithKit(UiToggle, { props: { modelValue: false, label: 'Lock', loading: true } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('moves the knob and paints the track primary when on', () => {
    const on = mountWithKit(UiToggle, { props: { modelValue: true, label: 'Lock' } })
    const off = mountWithKit(UiToggle, { props: { modelValue: false, label: 'Lock' } })

    expect(on.find('button').classes()).toContain('bg-primary-500')
    expect(on.find('button').classes()).not.toContain('bg-gray-400')
    expect(off.find('button').classes()).toContain('bg-gray-400')
    expect(off.find('button').classes()).toContain('border-gray-500')
    expect(on.find('span').classes()).toContain('translate-x-6')
    expect(off.find('span').classes()).toContain('translate-x-0')
  })

  it('does not emit when disabled', async () => {
    const wrapper = mountWithKit(UiToggle, { props: { modelValue: false, label: 'Lock', disabled: true } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})
