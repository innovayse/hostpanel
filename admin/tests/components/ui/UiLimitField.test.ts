/**
 * Unit tests for UiLimitField: the Unlimited switch emits the API's own unlimited value, a switched
 * off field puts back the earlier limit, and a megabyte limit shows its human size.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiLimitField from '@/components/ui/UiLimitField.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Props the tests vary. */
interface LimitProps {
  /** The limit or the unlimited value. */
  modelValue: number | null
  /** What the API calls unlimited. */
  unlimitedValue: number | null
  /** Megabyte limit with a human size line. */
  sizeMb?: boolean
  /** Disables the field. */
  disabled?: boolean
}

/**
 * Mounts the field with the English catalogue.
 *
 * @param props - Props for the field.
 * @returns The wrapper.
 */
const mountField = (props: LimitProps) =>
  mount(UiLimitField, { props, global: { plugins: [createTestI18n()] } })

describe('UiLimitField', () => {
  it('shows the limit in the number field with the switch off', () => {
    const wrapper = mountField({ modelValue: 25, unlimitedValue: null })

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('25')
    expect(wrapper.get('[data-testid="ui-limit-toggle"]').attributes('aria-checked')).toBe('false')
  })

  it('reads the API unlimited value as the switch on, with an empty disabled number field', () => {
    const wrapper = mountField({ modelValue: 0, unlimitedValue: 0 })

    expect(wrapper.get('[data-testid="ui-limit-toggle"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
    expect(wrapper.get('input').attributes('placeholder')).toBe('Unlimited')
  })

  it('emits null when the API means unlimited by null', async () => {
    const wrapper = mountField({ modelValue: 25, unlimitedValue: null })

    await wrapper.get('[data-testid="ui-limit-toggle"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('emits the number when the API means unlimited by 0', async () => {
    const wrapper = mountField({ modelValue: 512, unlimitedValue: 0 })

    await wrapper.get('[data-testid="ui-limit-toggle"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([0])
  })

  it('puts back the earlier limit when the switch goes off', async () => {
    const wrapper = mountField({ modelValue: 25, unlimitedValue: null })

    await wrapper.get('[data-testid="ui-limit-toggle"]').trigger('click')
    await wrapper.setProps({ modelValue: null })
    await wrapper.get('[data-testid="ui-limit-toggle"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[1]).toEqual([25])
  })

  it('starts from the first step when it never held a limit', async () => {
    const wrapper = mountField({ modelValue: null, unlimitedValue: null })

    await wrapper.get('[data-testid="ui-limit-toggle"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([1])
  })

  it('follows a limit typed into the number field', async () => {
    const wrapper = mountField({ modelValue: 25, unlimitedValue: null })

    await wrapper.get('input').setValue('40')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([40])
  })

  it('writes the human size of a megabyte limit and none when unlimited', async () => {
    const wrapper = mountField({ modelValue: 1536, unlimitedValue: 0, sizeMb: true })

    expect(wrapper.get('[data-testid="ui-limit-size"]').text()).toBe('1.5 GB')

    await wrapper.setProps({ modelValue: 0 })

    expect(wrapper.find('[data-testid="ui-limit-size"]').exists()).toBe(false)
  })

  it('disables both controls', () => {
    const wrapper = mountField({ modelValue: 25, unlimitedValue: null, disabled: true })

    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="ui-limit-toggle"]').attributes('disabled')).toBeDefined()
  })
})
