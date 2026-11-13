/**
 * Unit tests for UiDateRangePicker: the range field, its preset list and emitted ranges.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the picker with the English catalogue and the teleport inlined.
 *
 * @param modelValue - ISO range, or null for none.
 * @returns The wrapper.
 */
const mountPicker = (modelValue: [string, string] | null) =>
  mount(UiDateRangePicker, {
    props: { modelValue },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('UiDateRangePicker', () => {
  it('has a range format placeholder', () => {
    expect(mountPicker(null).get('input').attributes('placeholder')).toBe('DD/MM/YYYY - DD/MM/YYYY')
  })

  it('opens the preset list on click', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')

    const captions = wrapper.findAll('button').map(b => b.text())
    expect(captions).toEqual(expect.arrayContaining(['Today', 'Yesterday', 'Last 7 Days', 'This Month']))
  })

  it('emits a one-day range for the Today preset and shows it in the field', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Today')?.trigger('click')

    const range = wrapper.emitted('update:modelValue')?.[0]?.[0] as [string, string]
    expect(range[0]).toBe(range[1])
    expect(range[0]).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    expect((wrapper.get('input').element as HTMLInputElement).value).toContain(' - ')
  })

  it('emits null when the field is cleared', async () => {
    const wrapper = mountPicker(['2024-01-01', '2024-01-31'])

    await wrapper.get('input').setValue('')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
  })

  it('emits the range of a complete typed range and shows it normalised', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').setValue('01012024-31012024')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([['2024-01-01', '2024-01-31']])
    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('01/01/2024 - 31/01/2024')
  })

  it('ignores a typed range whose end precedes its start', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').setValue('31012024-01012024')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('shows the model in the field on mount', async () => {
    const wrapper = mountPicker(['2024-01-01', '2024-01-31'])
    await flushPromises()

    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('01/01/2024 - 31/01/2024')
  })

  it('applies a range picked from the grid, putting the earlier click first', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')
    // Click the later day first: the picker must swap them.
    await wrapper.findAll('button').filter(b => b.text() === '20')[0]?.trigger('click')
    await wrapper.findAll('button').filter(b => b.text() === '10')[0]?.trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Apply')?.trigger('click')

    const range = wrapper.emitted('update:modelValue')?.[0]?.[0] as [string, string]
    expect(range[0].endsWith('-10')).toBe(true)
    expect(range[1].endsWith('-20')).toBe(true)
  })

  it('does not apply until both ends are picked', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Apply')?.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('clears the range from the popup', async () => {
    const wrapper = mountPicker(['2024-01-01', '2024-01-31'])

    await wrapper.get('input').trigger('click')
    await wrapper.findAll('button').find(b => b.text() === 'Clear')?.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([null])
    expect((wrapper.get('input').element as HTMLInputElement).value).toBe('')
  })

  it('positions the popup through CSS-variable classes, not an inline style', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')

    const popup = wrapper.find('div.fixed')
    expect(popup.classes()).toContain('top-[var(--fp-top,auto)]')
    expect(popup.classes()).toContain('left-[var(--fp-left,auto)]')
  })

  it('exposes a testid on the field and every popup control', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('[data-testid="date-range-input"]').trigger('click')

    for (const id of ['date-range-preset-today', 'date-range-prev', 'date-range-next', 'date-range-clear', 'date-range-apply']) {
      expect(wrapper.find(`[data-testid="${id}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="date-range-day-0-0"]').exists()).toBe(true)
  })
})
