/**
 * Unit tests for UiNumberInput: stepping, clamping, typing and the disabled state.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'

describe('UiNumberInput', () => {
  it('shows the model value and the placeholder for an empty one', () => {
    expect((mount(UiNumberInput, { props: { modelValue: 5 } }).get('input').element as HTMLInputElement).value).toBe('5')
    expect(mount(UiNumberInput, { props: { modelValue: null } }).get('input').attributes('placeholder')).toBe('0')
  })

  it('steps up and down by the step', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 5, step: 2 } })

    await wrapper.get('[data-testid="number-increment"]').trigger('mousedown')
    await wrapper.get('[data-testid="number-decrement"]').trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')).toEqual([[7], [3]])
  })

  it('avoids floating point drift', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 0.1, step: 0.2 } })

    await wrapper.get('[data-testid="number-increment"]').trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([0.3])
  })

  it('does not step past max or min', async () => {
    const atMax = mount(UiNumberInput, { props: { modelValue: 10, max: 10 } })
    const atMin = mount(UiNumberInput, { props: { modelValue: 0, min: 0 } })

    await atMax.get('[data-testid="number-increment"]').trigger('mousedown')
    await atMin.get('[data-testid="number-decrement"]').trigger('mousedown')

    expect(atMax.emitted('update:modelValue')).toBeUndefined()
    expect(atMin.emitted('update:modelValue')).toBeUndefined()
    expect(atMax.get('[data-testid="number-increment"]').attributes('disabled')).toBeDefined()
  })

  it('clamps typed numbers and ignores text', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 1, min: 0, max: 10 } })

    await wrapper.get('input').setValue('15')
    await wrapper.get('input').setValue('abc')

    expect(wrapper.emitted('update:modelValue')).toEqual([[10]])
  })

  it('treats an empty model as zero when stepping', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: '' } })

    await wrapper.get('[data-testid="number-increment"]').trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([1])
  })

  it('emits nothing and stays empty when blurred while empty', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: null, min: 0 } })

    await wrapper.get('input').trigger('blur')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('')
  })

  it('emits null when a nullable field is cleared and ignores a cleared required one', async () => {
    const nullable = mount(UiNumberInput, { props: { modelValue: 5, nullable: true } })
    const required = mount(UiNumberInput, { props: { modelValue: 5 } })

    await nullable.get('input').setValue('')
    await required.get('input').setValue('')

    expect(nullable.emitted('update:modelValue')).toEqual([[null]])
    expect(required.emitted('update:modelValue')).toBeUndefined()
  })

  it('clamps a typed value under min on blur', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: -3, min: 0 } })

    await wrapper.get('input').trigger('blur')

    expect(wrapper.emitted('update:modelValue')).toEqual([[0]])
  })

  it('ignores everything while disabled', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 5, disabled: true } })

    await wrapper.get('[data-testid="number-increment"]').trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
  })

  it('puts the id on the input and flags it invalid', () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 5, id: 'amount', invalid: true } })

    expect(wrapper.get('input').attributes('id')).toBe('amount')
    expect(wrapper.get('input').attributes('aria-invalid')).toBe('true')
    expect(wrapper.get('input').classes()).toContain('border-status-red/60')
  })

  it('is not flagged invalid by default', () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 5 } })

    expect(wrapper.get('input').attributes('aria-invalid')).toBeUndefined()
  })

  it('ties the field to its message and shows a disabled look without opacity', () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 1, describedby: 'msg', invalid: true, disabled: true } })
    const input = wrapper.get('input')

    expect(input.attributes('aria-describedby')).toBe('msg')
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(input.attributes('disabled')).toBeDefined()
    expect(input.classes()).toEqual(expect.arrayContaining(['disabled:text-text-muted', 'rounded-control']))
    expect(wrapper.classes()).not.toContain('opacity-50')
  })
})
