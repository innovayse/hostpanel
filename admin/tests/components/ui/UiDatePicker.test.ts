/**
 * Unit tests for UiDatePicker: typed dates, the displayed value, both quick-choice directions,
 * grid selection, clearing and the disabled state.
 */
import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import type { UiDatePickerDirection } from '@/utils/ui/datePicker'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the picker with the English catalogue.
 *
 * @param modelValue - ISO date, or null for none.
 * @param extra - Further props (direction, placeholder, disabled).
 * @returns The wrapper.
 */
const mountPicker = (
  modelValue: string | null,
  extra: { direction?: UiDatePickerDirection; placeholder?: string; disabled?: boolean } = {},
) => mount(UiDatePicker, { props: { modelValue, ...extra }, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

/**
 * Captions of every button in the wrapper.
 *
 * @param wrapper - The mounted picker.
 * @returns Trimmed button texts.
 */
const captions = (wrapper: ReturnType<typeof mountPicker>): string[] => wrapper.findAll('button').map(b => b.text())

describe('UiDatePicker', () => {
  it('shows the ISO model as day/month/year', async () => {
    const wrapper = mountPicker('2024-03-15')
    await nextTick()

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('15/03/2024')
  })

  it('follows an outside change of the model', async () => {
    const wrapper = mountPicker('2024-03-15')
    await wrapper.setProps({ modelValue: '2025-01-02' })

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('02/01/2025')
  })

  it('falls back to the format placeholder when none or an empty one is given', () => {
    expect(mountPicker(null).get('input').attributes('placeholder')).toBe('DD/MM/YYYY')
    expect(mountPicker(null, { placeholder: '' }).get('input').attributes('placeholder')).toBe('DD/MM/YYYY')
  })

  it('uses the caller placeholder', () => {
    expect(mountPicker(null, { placeholder: 'Pick' }).get('input').attributes('placeholder')).toBe('Pick')
  })

  it('emits an ISO date for a complete typed date', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').setValue('15032024')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['2024-03-15'])
  })

  it('emits nothing for a partial date', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').setValue('1503')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('emits null when the field is cleared', async () => {
    const wrapper = mountPicker('2024-03-15')

    await wrapper.get('input').setValue('')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('opens the popup on click and emits today from the quick button', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Today')?.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]?.[0]).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('offers the past quick choices by default', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')

    expect(captions(wrapper)).toEqual(expect.arrayContaining(['Yesterday', '7 Days Ago', '1 Month Ago', '1 Year Ago']))
  })

  it('offers the future quick choices when the direction is future', async () => {
    const wrapper = mountPicker(null, { direction: 'future' })

    await wrapper.get('input').trigger('click')

    expect(captions(wrapper)).toEqual(expect.arrayContaining(['Tomorrow', 'In 7 Days']))
    expect(captions(wrapper)).not.toContain('Yesterday')
  })

  it('emits a day after today for the future "Tomorrow" choice', async () => {
    const wrapper = mountPicker(null, { direction: 'future' })

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Tomorrow')?.trigger('click')

    const emitted = wrapper.emitted('update:modelValue')?.[0]?.[0] as string
    expect(new Date(emitted).getTime()).toBeGreaterThan(Date.now() - 24 * 3600 * 1000)
  })

  it('picks a day from the grid and closes the popup', async () => {
    const wrapper = mountPicker('2024-03-15')
    await nextTick()

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === '20')?.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['2024-03-20'])
    expect(captions(wrapper)).not.toContain('Today')
  })

  it('clears the date from the popup', async () => {
    const wrapper = mountPicker('2024-03-15')

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Clear')?.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('does not open when disabled', async () => {
    const wrapper = mountPicker(null, { disabled: true })

    await wrapper.get('input').trigger('click')

    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
    expect(captions(wrapper)).toHaveLength(0)
  })

  it('exposes a testid on the field and every popup control', async () => {
    const wrapper = mountPicker('2024-03-15')
    await nextTick()

    await wrapper.get('[data-testid="date-picker-input"]').trigger('click')

    for (const id of ['date-picker-quick-today', 'date-picker-prev', 'date-picker-next', 'date-picker-clear', 'date-picker-close']) {
      expect(wrapper.find(`[data-testid="${id}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="date-picker-day-0"]').exists()).toBe(true)
  })

  it('draws the popup in the page body, fixed under the field', async () => {
    const wrapper = mount(UiDatePicker, { props: { modelValue: null }, attachTo: document.body, global: { plugins: [createTestI18n()] } })

    await wrapper.get('input').trigger('click')

    const popup = document.body.querySelector('[data-testid="date-picker-prev"]')?.closest('.fixed')
    expect(popup).not.toBeNull()
    expect(wrapper.element.contains(popup ?? null)).toBe(false)
    wrapper.unmount()
  })

  it('closes on a pointer-down outside the field and the popup, not inside the popup', async () => {
    const wrapper = mount(UiDatePicker, { props: { modelValue: null }, attachTo: document.body, global: { plugins: [createTestI18n()] } })
    await wrapper.get('input').trigger('click')

    document.body.querySelector('[data-testid="date-picker-prev"]')?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(document.body.querySelector('[data-testid="date-picker-prev"]')).not.toBeNull()

    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(document.body.querySelector('[data-testid="date-picker-prev"]')).toBeNull()
    wrapper.unmount()
  })
})
