/**
 * Unit tests for UiDateRangePicker: the range field, its preset list and emitted ranges.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import { formatIsoDate } from '@/utils/ui/datePicker'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

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

  it('shows the selected range in the footer and opens the calendar at its start month', async () => {
    const wrapper = mountPicker(['2026-01-01', '2026-12-31'])

    await wrapper.get('input').trigger('click')

    expect(wrapper.text()).toContain('01/01/2026 - 31/12/2026')
    expect(wrapper.text()).not.toContain('Select date range')
    expect(wrapper.text()).toContain('January')
  })

  it('highlights the range days with start and end caps', async () => {
    const wrapper = mountPicker(['2026-01-05', '2026-01-09'])

    await wrapper.get('input').trigger('click')

    const days = wrapper.findAll('[data-testid^="date-range-day-0-"]').filter(d => /^\d+$/.test(d.text()))
    /** Finds the grid cell showing a day number. */
    const byDay = (n: string) => days.find(d => d.text() === n)
    expect(byDay('5')?.classes()).toContain('rounded-r-none')
    expect(byDay('9')?.classes()).toContain('rounded-l-none')
    expect(byDay('7')?.classes()).toContain('rounded-none')
    expect(byDay('12')?.classes()).not.toContain('rounded-none')
  })

  it('marks the preset the range stands for, and Custom otherwise', async () => {
    const todayIso = formatIsoDate(new Date())
    const preset = mountPicker([todayIso, todayIso])
    await preset.get('input').trigger('click')
    expect(preset.get('[data-testid="date-range-preset-today"]').attributes('aria-pressed')).toBe('true')
    expect(preset.get('[data-testid="date-range-preset-custom"]').classes()).not.toContain('text-surface-base')

    const custom = mountPicker(['2020-02-03', '2020-02-05'])
    await custom.get('input').trigger('click')
    expect(custom.get('[data-testid="date-range-preset-today"]').attributes('aria-pressed')).toBe('false')
    expect(custom.get('[data-testid="date-range-preset-custom"]').classes()).toContain('text-surface-base')
  })

  it('keeps preset captions on one line and the footer free of a translucent fill', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')

    expect(wrapper.get('[data-testid="date-range-preset-today"]').classes()).toContain('whitespace-nowrap')
    expect(wrapper.get('[data-testid="date-range-apply"]').element.closest('.border-t')?.classList.contains('bg-surface-hover')).toBe(false)
  })

  it('shows one calendar on a phone: the second month and its selectors are hidden below md', async () => {
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('click')

    expect(wrapper.get('[data-testid="date-range-month-0"]').classes()).not.toContain('max-md:hidden')
    expect(wrapper.get('[data-testid="date-range-month-1"]').classes()).toContain('max-md:hidden')
    expect(wrapper.get('[data-testid="date-range-next-selects"]').classes()).toContain('max-md:hidden')
  })
})

describe('UiDateRangePicker on a phone', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens the presets and the calendar as a bottom sheet by a tap, with its Apply and Clear inside', async () => {
    stubPhone(true)
    const wrapper = mountPicker(null)

    expect(wrapper.get('input').attributes('inputmode')).toBe('none')
    await wrapper.get('input').trigger('click')

    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="date-range-apply"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="date-range-clear"]').exists()).toBe(true)
  })

  it('does not open on focus, and commits a preset from the sheet', async () => {
    stubPhone(true)
    const wrapper = mountPicker(null)

    await wrapper.get('input').trigger('focus')
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)

    await wrapper.get('input').trigger('click')
    await wrapper.get('[data-testid="date-range-preset-today"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toHaveLength(1)
  })

  it('opens on focus and leaves the keyboard alone on a wide screen', async () => {
    stubPhone(false)
    const wrapper = mountPicker(null)

    expect(wrapper.get('input').attributes('inputmode')).toBeUndefined()
    await wrapper.get('input').trigger('focus')

    expect(wrapper.find('[data-testid="date-range-prev"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
  })
})
