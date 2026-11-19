/**
 * Unit tests for UiDateTimePicker: the library is replaced by a stub that exposes the props the
 * kit passes and lets the test report a value.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useThemeStore } from '@/stores/theme'
import UiDateTimePicker from '@/components/ui/UiDateTimePicker.vue'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

vi.mock('@vuepic/vue-datepicker', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    VueDatePicker: defineComponent({
      name: 'VueDatePicker',
      props: ['modelValue', 'placeholder', 'minDate', 'inputClassName', 'clearable', 'dark', 'autoApply', 'enableTimePicker', 'inline'],
      emits: ['update:modelValue'],
      setup: (props, { emit }) => () =>
        h('div', { 'data-testid': 'stub', 'data-inline': String(props.inline !== undefined && props.inline !== false), 'data-placeholder': props.placeholder, 'data-class': props.inputClassName }, [
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
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null, placeholder: 'No start date' }, global: { plugins: [createTestI18n()] } })
    const stub = wrapper.get('[data-testid="stub"]')

    expect(stub.attributes('data-placeholder')).toBe('No start date')
    expect(stub.attributes('data-class')).toContain('bg-surface-field!')
  })

  it('follows the panel scheme: the library is dark unless the light palette is applied', () => {
    const theme = useThemeStore()
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null }, global: { plugins: [createTestI18n()] } })

    theme.isLight = true

    return wrapper.vm.$nextTick().then(() => {
      expect(wrapper.findComponent({ name: 'VueDatePicker' }).props('dark')).toBe(false)
    })
  })

  it('emits the picked moment as a Date', async () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null }, global: { plugins: [createTestI18n()] } })

    await wrapper.get('[data-testid="pick"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([new Date(2026, 0, 2)])
  })

  it('emits null when the value is cleared', async () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: new Date(2026, 0, 2) }, global: { plugins: [createTestI18n()] } })

    await wrapper.get('[data-testid="clear"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('themes the library through its CSS variables on a wrapper', () => {
    const wrapper = mount(UiDateTimePicker, { props: { modelValue: null }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.classes()).toContain('[&_.dp--main]:[--dp-background-color:var(--surface-elevated)]')
  })
})

describe('UiDateTimePicker on a phone', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /**
   * Mounts the picker with the English catalogue and the teleport inlined.
   *
   * @param modelValue - The chosen moment.
   * @param placeholder - The hint of the field.
   * @returns The wrapper.
   */
  const mountPhone = (modelValue: Date | null, placeholder?: string) => {
    setActivePinia(createPinia())
    return mount(UiDateTimePicker, { props: { modelValue, placeholder }, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })
  }

  it('shows a field button with the hint and no library popup until it is pressed', () => {
    stubPhone(true)
    const wrapper = mountPhone(null, 'No start date')

    expect(wrapper.get('[data-testid="date-time-trigger"]').text()).toBe('No start date')
    expect(wrapper.find('[data-testid="stub"]').exists()).toBe(false)
  })

  it('opens the calendar inline in a bottom sheet, titled with the hint, and picks a moment', async () => {
    stubPhone(true)
    const wrapper = mountPhone(null, 'No start date')

    await wrapper.get('[data-testid="date-time-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.get('h2').text()).toBe('No start date')
    expect(wrapper.get('[data-testid="stub"]').attributes('data-inline')).toBe('true')

    await wrapper.get('[data-testid="pick"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([new Date(2026, 0, 2)])
  })

  it('shows the chosen moment on the field, clears it from the sheet and closes', async () => {
    stubPhone(true)
    const wrapper = mountPhone(new Date(2026, 0, 2, 9, 30))

    expect(wrapper.get('[data-testid="date-time-trigger"]').text()).toContain('2026')
    await wrapper.get('[data-testid="date-time-trigger"]').trigger('click')
    await wrapper.get('[data-testid="date-time-clear"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
  })

  it('closes from Apply, and falls back to a generic title without a hint', async () => {
    stubPhone(true)
    const wrapper = mountPhone(null)

    await wrapper.get('[data-testid="date-time-trigger"]').trigger('click')
    expect(wrapper.get('h2').text()).toBe('Select date and time')
    await wrapper.get('[data-testid="date-time-apply"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
  })

  it('keeps the library popup on a wide screen', () => {
    stubPhone(false)
    const wrapper = mountPhone(null)

    expect(wrapper.find('[data-testid="date-time-trigger"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="stub"]').attributes('data-inline')).toBe('false')
  })
})
