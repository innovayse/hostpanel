/**
 * Unit tests for TransactionFilterFields: the direction and payment method selects with an "any" choice first, the
 * period range picker and the amount input, and the chosen values written back to the draft (the amount as text,
 * empty for none).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TransactionFilterFields from '@/components/billing/TransactionFilterFields.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { PaymentMethod, TransactionDirection } from '@/types/transaction'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the fields with an empty draft, or with the given amount.
 *
 * @param amount - The amount the draft starts with, as text.
 * @returns The wrapper.
 */
const mountFields = (amount = '') =>
  mount(TransactionFilterFields, {
    props: { direction: '', paymentMethod: '', period: null, amount, testidPrefix: 'transactions-filter' },
    global: { plugins: [createTestI18n()] },
  })

/**
 * Finds one of the selects by the suffix of its test id.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `direction` or `method`.
 * @returns The select.
 */
const selectOf = (wrapper: ReturnType<typeof mountFields>, suffix: string) =>
  wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === `transactions-filter-${suffix}`)

/**
 * Reads the options one of the selects offers. UiSelect is generic, so its props are typed through a cast.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `direction` or `method`.
 * @returns The options the select offers.
 */
const optionsOf = (wrapper: ReturnType<typeof mountFields>, suffix: string): UiSelectOption<string>[] => {
  const select = selectOf(wrapper, suffix)
  if (select === undefined) {
    throw new Error(`No select ends in ${suffix}`)
  }
  return (select.props() as unknown as { options: UiSelectOption<string>[] }).options
}

describe('TransactionFilterFields', () => {
  it('offers all activity first, then money in and money out', () => {
    const options = optionsOf(mountFields(), 'direction')

    expect(options.map((option) => option.value)).toEqual(['', TransactionDirection.In, TransactionDirection.Out])
    expect(options[0]).toEqual({ value: '', label: 'All Activity' })
  })

  it('offers any first, then the payment methods a ledger entry can name', () => {
    const options = optionsOf(mountFields(), 'method')

    expect(options[0]).toEqual({ value: '', label: 'Any' })
    expect(options.slice(1).map((option) => option.value)).toContain(PaymentMethod.Card)
  })

  it('writes the chosen direction and payment method back to the draft', () => {
    const wrapper = mountFields()

    selectOf(wrapper, 'direction')?.vm.$emit('update:modelValue', 'Out')
    selectOf(wrapper, 'method')?.vm.$emit('update:modelValue', PaymentMethod.Card)

    expect(wrapper.emitted('update:direction')).toEqual([['Out']])
    expect(wrapper.emitted('update:paymentMethod')).toEqual([[PaymentMethod.Card]])
  })

  it('writes the picked period back to the draft', () => {
    const wrapper = mountFields()
    const picker = wrapper.findComponent(UiDateRangePicker)

    picker.vm.$emit('update:modelValue', ['2026-03-01', '2026-03-31'])

    expect(picker.attributes('data-testid')).toBe('transactions-filter-period')
    expect(wrapper.emitted('update:period')).toEqual([[['2026-03-01', '2026-03-31']]])
  })

  it('shows the draft amount in the number input, empty while none is set', () => {
    const empty = mountFields().findComponent(UiNumberInput)
    const set = mountFields('12.5').findComponent(UiNumberInput)

    expect(empty.attributes('data-testid')).toBe('transactions-filter-amount')
    expect(empty.props('modelValue')).toBeNull()
    expect(set.props('modelValue')).toBe(12.5)
  })

  it('writes the typed amount back as text, and clears it when the field is emptied', () => {
    const wrapper = mountFields()
    const input = wrapper.findComponent(UiNumberInput)

    input.vm.$emit('update:modelValue', 40)
    input.vm.$emit('update:modelValue', null)

    expect(wrapper.emitted('update:amount')).toEqual([['40'], ['']])
  })

  it('clears the amount when it is set to zero, which is no amount', () => {
    const wrapper = mountFields('5')

    wrapper.findComponent(UiNumberInput).vm.$emit('update:modelValue', 0)

    expect(wrapper.emitted('update:amount')).toEqual([['']])
  })
})
