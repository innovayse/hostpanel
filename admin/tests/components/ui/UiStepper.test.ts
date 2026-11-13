/**
 * Unit tests for UiStepper: display, stepping, press-and-hold repeat and typed input.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiStepper from '@/components/ui/UiStepper.vue'

afterEach(() => {
  vi.useRealTimers()
})

describe('UiStepper', () => {
  it('shows two decimals, and nothing for zero', () => {
    expect((mount(UiStepper, { props: { modelValue: 12.5 } }).get('input').element as HTMLInputElement).value).toBe('12.50')
    expect((mount(UiStepper, { props: { modelValue: 0 } }).get('input').element as HTMLInputElement).value).toBe('')
  })

  it('draws the kit field box with room for the steppers', () => {
    const input = mount(UiStepper, { props: { modelValue: 1 } }).get('input')

    expect(input.classes()).toEqual(expect.arrayContaining(['bg-surface-field', 'border-border-control', 'pr-[68px]']))
  })

  it('steps once on press', async () => {
    const wrapper = mount(UiStepper, { props: { modelValue: 1, step: 0.5 } })

    await wrapper.get('[data-testid="stepper-increment"]').trigger('mousedown')
    await wrapper.get('[data-testid="stepper-decrement"]').trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')).toEqual([[1.5], [0.5]])
  })

  it('keeps stepping while held and stops on release', async () => {
    vi.useFakeTimers()
    const wrapper = mount(UiStepper, { props: { modelValue: 0, step: 1 } })
    const up = wrapper.get('[data-testid="stepper-increment"]')

    await up.trigger('mousedown')
    vi.advanceTimersByTime(250)
    await up.trigger('mouseup')
    vi.advanceTimersByTime(500)

    expect(wrapper.emitted('update:modelValue')).toHaveLength(3)
  })

  it('stops stepping when unmounted mid-hold', async () => {
    vi.useFakeTimers()
    const wrapper = mount(UiStepper, { props: { modelValue: 0 } })

    await wrapper.get('[data-testid="stepper-increment"]').trigger('mousedown')
    const before = wrapper.emitted('update:modelValue')?.length ?? 0
    wrapper.unmount()
    vi.advanceTimersByTime(500)

    expect(before).toBe(1)
    expect(vi.getTimerCount()).toBe(0)
  })

  it('does not step past max or min', async () => {
    const atMax = mount(UiStepper, { props: { modelValue: 5, max: 5 } })
    const atMin = mount(UiStepper, { props: { modelValue: 0, min: 0 } })

    await atMax.get('[data-testid="stepper-increment"]').trigger('mousedown')
    await atMin.get('[data-testid="stepper-decrement"]').trigger('mousedown')

    expect(atMax.emitted('update:modelValue')).toBeUndefined()
    expect(atMin.emitted('update:modelValue')).toBeUndefined()
  })

  it('parses typed numbers, treats empty as zero and ignores text', async () => {
    const wrapper = mount(UiStepper, { props: { modelValue: 3 } })

    await wrapper.get('input').setValue('7.25')
    await wrapper.get('input').setValue('')

    expect(wrapper.emitted('update:modelValue')).toEqual([[7.25], [0]])
  })

  it('resets an empty field to zero on blur', async () => {
    const wrapper = mount(UiStepper, { props: { modelValue: 0 } })

    await wrapper.get('input').trigger('blur')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([0])
  })
})
