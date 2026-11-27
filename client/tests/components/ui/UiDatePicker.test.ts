/**
 * Unit tests for UiDatePicker: three kit lists make one ISO date, a day the month does not have is dropped, and a model set
 * from outside fills the lists.
 *
 * @module tests/components/ui/UiDatePicker.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiDatePicker from '~/components/ui/UiDatePicker.vue'

/**
 * Picks one option of one of the three lists.
 *
 * @param wrapper - The mounted picker.
 * @param part - The list: day, month or year.
 * @param value - The option's value.
 */
const pick = async (wrapper: ReturnType<typeof mountWithKit>, part: string, value: string): Promise<void> => {
  await wrapper.find('[data-testid="date-' + part + '"]').trigger('click')
  await wrapper.find('[data-testid="ui-select-option-' + value + '"]').trigger('click')
}

describe('UiDatePicker', () => {
  it('shows the three lists with their placeholders and the label', () => {
    const wrapper = mountWithKit(UiDatePicker, { props: { label: 'Valid until', testid: 'date' } })

    expect(wrapper.text()).toContain('Valid until')
    expect(wrapper.find('[data-testid="date-day"]').text()).toBe('ui.datePicker.day')
    expect(wrapper.find('[data-testid="date-month"]').text()).toBe('ui.datePicker.month')
    expect(wrapper.find('[data-testid="date-year"]').text()).toBe('ui.datePicker.year')
  })

  it('shows the parts of the model it is given', () => {
    const wrapper = mountWithKit(UiDatePicker, { props: { modelValue: '2026-03-09', testid: 'date' } })

    expect(wrapper.find('[data-testid="date-day"]').text()).toBe('9')
    expect(wrapper.find('[data-testid="date-year"]').text()).toBe('2026')
  })

  it('reports the ISO date once the three parts name a day', async () => {
    const wrapper = mountWithKit(UiDatePicker, { props: { testid: 'date' } })

    await pick(wrapper, 'year', '2026')
    await pick(wrapper, 'month', '03')
    await pick(wrapper, 'day', '09')

    expect(wrapper.emitted('update:modelValue')?.map(args => args[0])).toEqual(['', '', '2026-03-09'])
  })

  it('drops a day the chosen month does not have', async () => {
    const wrapper = mountWithKit(UiDatePicker, { props: { modelValue: '2026-03-31', testid: 'date' } })

    await pick(wrapper, 'month', '04')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([''])
    expect(wrapper.find('[data-testid="date-day"]').text()).toBe('ui.datePicker.day')
  })

  it('shows the error line and not the hint', () => {
    const wrapper = mountWithKit(UiDatePicker, { props: { error: 'Wrong date.', hint: 'A hint.' } })

    expect(wrapper.text()).toContain('Wrong date.')
    expect(wrapper.text()).not.toContain('A hint.')
  })
})
