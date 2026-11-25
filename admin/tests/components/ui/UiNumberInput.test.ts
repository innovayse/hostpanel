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

  it('writes the unit inside the field and leaves room for it, none by default', () => {
    const plain = mount(UiNumberInput, { props: { modelValue: 5 } })
    const withUnit = mount(UiNumberInput, { props: { modelValue: 5, suffix: 'min' } })

    expect(plain.find('[data-testid="number-suffix"]').exists()).toBe(false)
    expect(plain.get('input').classes()).toContain('pr-8')
    expect(withUnit.get('[data-testid="number-suffix"]').text()).toBe('min')
    expect(withUnit.get('input').classes()).toContain('pr-20')
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

  it('keeps a minimum width that fits the value and the spinner, wider with a suffix', () => {
    expect(mount(UiNumberInput, { props: { modelValue: 1 } }).classes()).toContain('min-w-28')
    expect(mount(UiNumberInput, { props: { modelValue: 1, suffix: 'min' } }).classes()).toContain('min-w-40')
  })

  it('keeps the spinner column inside the input border, with theme tokens only', () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 1 } })
    const column = wrapper.get('[data-testid="number-increment"]').element.parentElement as HTMLElement

    expect(column.className).toContain('right-px')
    expect(column.className).toContain('border-border-control')
    expect(column.className).toContain('overflow-hidden')
    expect(column.className).not.toContain('bg-surface-elevated')
    expect(wrapper.get('input').classes()).toEqual(expect.arrayContaining(['border-border-control', 'bg-surface-field', 'rounded-control', 'h-10.5']))
    expect(wrapper.get('[data-testid="number-increment"]').classes()).toContain('bg-transparent')
  })

  it('shows a field that steps by fractions with two decimals once it is left', async () => {
    const wrapper = mount(UiNumberInput, { props: { modelValue: 23.7, step: 0.01 } })
    const input = wrapper.get('input')

    expect((input.element as HTMLInputElement).value).toBe('23.70')

    await input.trigger('focus')
    expect((input.element as HTMLInputElement).value).toBe('23.7')

    await input.trigger('blur')
    expect((input.element as HTMLInputElement).value).toBe('23.70')
  })

  it('writes a typed amount in two decimals on blur and leaves a whole-number field alone', async () => {
    const money = mount(UiNumberInput, { props: { modelValue: 5, step: 0.01 } })
    const whole = mount(UiNumberInput, { props: { modelValue: 5, step: 1 } })

    await money.get('input').setValue('7.5')
    await money.get('input').trigger('blur')

    expect((money.get('input').element as HTMLInputElement).value).toBe('7.50')
    expect((whole.get('input').element as HTMLInputElement).value).toBe('5')
  })

  it('shows no decimals for an empty value of a money field', () => {
    expect((mount(UiNumberInput, { props: { modelValue: null, step: 0.01 } }).get('input').element as HTMLInputElement).value).toBe('')
  })

  it('marks the input required for the browser and assistive tech only when asked', () => {
    const required = mount(UiNumberInput, { props: { modelValue: null, required: true } }).get('input')
    const optional = mount(UiNumberInput, { props: { modelValue: null } }).get('input')

    expect(required.attributes('required')).toBeDefined()
    expect(required.attributes('aria-required')).toBe('true')
    expect(optional.attributes('required')).toBeUndefined()
    expect(optional.attributes('aria-required')).toBeUndefined()
  })
})
