/**
 * Unit tests for UiRadio: v-model selection, the value it stands for, disabled state and
 * attribute pass-through.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiRadio from '@/components/ui/UiRadio.vue'

describe('UiRadio', () => {
  it('renders a native radio input', () => {
    const wrapper = mount(UiRadio, { props: { value: 'a' } })

    expect(wrapper.element.tagName).toBe('INPUT')
    expect(wrapper.attributes('type')).toBe('radio')
  })

  it('is checked when the model equals its value', () => {
    const wrapper = mount(UiRadio, { props: { value: 'a', modelValue: 'a' } })

    expect((wrapper.element as HTMLInputElement).checked).toBe(true)
  })

  it('compares boolean values strictly', () => {
    const wrapper = mount(UiRadio, { props: { value: false, modelValue: false } })

    expect((wrapper.element as HTMLInputElement).checked).toBe(true)
  })

  it('emits its value when picked', async () => {
    const wrapper = mount(UiRadio, { props: { value: 'b', modelValue: 'a' } })

    await wrapper.trigger('change')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
  })

  it('passes classes, name and data-testid through to the input', () => {
    const wrapper = mount(UiRadio, {
      props: { value: 'a' },
      attrs: { class: 'mt-1', name: 'group', 'data-testid': 'probe' },
    })

    expect(wrapper.classes()).toContain('mt-1')
    expect(wrapper.attributes('name')).toBe('group')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('can be disabled', () => {
    const wrapper = mount(UiRadio, { props: { value: 'a', disabled: true } })

    expect(wrapper.attributes('disabled')).toBeDefined()
  })

  it('is drawn as a field disc that fills with the brand gradient when selected', () => {
    const classes = mount(UiRadio, { props: { value: 'a' } }).classes()

    expect(classes).toContain('appearance-none')
    expect(classes).toContain('bg-surface-field')
    expect(classes).toContain('radio-dot')
  })
})
