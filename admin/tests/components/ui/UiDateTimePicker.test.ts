/**
 * Unit tests for UiDateTimePicker: the library is replaced by a stub that exposes the props the
 * kit passes and lets the test report a value.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useThemeStore } from '@/stores/theme'
import UiDateTimePicker from '@/components/ui/UiDateTimePicker.vue'

vi.mock('@vuepic/vue-datepicker', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    VueDatePicker: defineComponent({
      name: 'VueDatePicker',
      props: ['modelValue', 'placeholder', 'minDate', 'inputClassName', 'clearable', 'dark', 'autoApply', 'enableTimePicker'],
      emits: ['update:modelValue'],
      setup: (props, { emit }) => () =>
        h('div', { 'data-testid': 'stub', 'data-placeholder': props.placeholder, 'data-class': props.inputClassName }, [
          h('button', { 'data-testid': 'pick', onClick: () => emit('update:modelValue', new Date(2026, 0, 2)) }),
          h('button', { 'data-testid': 'clear', onClick: () => emit('update:modelValue', null) }),
        ]),
    }),
  }
})

describe('UiDateTimePicker', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('passes the placeholder and the themed input classes to the library', () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null, placeholder: 'No start date' } })
    const stub = wrapper.get('[data-testid="stub"]')

    expect(stub.attributes('data-placeholder')).toBe('No start date')
    expect(stub.attributes('data-class')).toContain('bg-surface-field!')
  })

  it('follows the panel scheme: the library is dark unless the light palette is applied', () => {
    const theme = useThemeStore()
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null } })

    theme.isLight = true

    return wrapper.vm.$nextTick().then(() => {
      expect(wrapper.findComponent({ name: 'VueDatePicker' }).props('dark')).toBe(false)
    })
  })

  it('emits the picked moment as a Date', async () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null } })

    await wrapper.get('[data-testid="pick"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([new Date(2026, 0, 2)])
  })

  it('emits null when the value is cleared', async () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: new Date(2026, 0, 2) } })

    await wrapper.get('[data-testid="clear"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('themes the library through its CSS variables on a wrapper', () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null } })

    expect(wrapper.classes()).toContain('[&_.dp--main]:[--dp-background-color:var(--surface-elevated)]')
  })
})
