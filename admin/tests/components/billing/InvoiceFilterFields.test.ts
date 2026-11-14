/**
 * Unit tests for InvoiceFilterFields: the status select with an "any" choice first and every invoice status after it, the
 * period range picker, and the chosen values written back to the draft.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import InvoiceFilterFields from '@/components/billing/InvoiceFilterFields.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { InvoiceStatus } from '@/types/invoice'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the fields with an empty draft.
 *
 * @returns The wrapper.
 */
const mountFields = () =>
  mount(InvoiceFilterFields, {
    props: { status: '', period: null, testidPrefix: 'invoices-filter' },
    global: { plugins: [createTestI18n()] },
  })

describe('InvoiceFilterFields', () => {
  it('offers any first, then every invoice status', () => {
    const wrapper = mountFields()
    const select = wrapper.findComponent(UiSelect)
    const options = (select.props() as unknown as { options: UiSelectOption<string>[] }).options

    expect(select.attributes('data-testid')).toBe('invoices-filter-status')
    expect(options[0]).toEqual({ value: '', label: 'Any' })
    expect(options.slice(1).map((option) => option.value).sort()).toEqual(Object.values(InvoiceStatus).sort())
  })

  it('writes the chosen status back to the draft', () => {
    const wrapper = mountFields()

    wrapper.findComponent(UiSelect).vm.$emit('update:modelValue', 'Paid')

    expect(wrapper.emitted('update:status')).toEqual([['Paid']])
  })

  it('writes the picked period back to the draft', () => {
    const wrapper = mountFields()
    const picker = wrapper.findComponent(UiDateRangePicker)

    picker.vm.$emit('update:modelValue', ['2026-10-01', '2026-10-31'])

    expect(picker.attributes('data-testid')).toBe('invoices-filter-period')
    expect(wrapper.emitted('update:period')).toEqual([[['2026-10-01', '2026-10-31']]])
  })
})
