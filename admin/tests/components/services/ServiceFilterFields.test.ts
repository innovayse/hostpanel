/**
 * Unit tests for ServiceFilterFields: the status, product and billing cycle selects with an "any" choice first, the
 * product choices named after the products, and the chosen values written back to the draft.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ServiceFilterFields from '@/components/services/ServiceFilterFields.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { Product } from '@/types/product'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/** The products the product field offers. */
const products = [
  { id: 1, name: 'Shared Starter' },
  { id: 2, name: 'VPS Small' },
] as Product[]

/**
 * Mounts the fields with an empty draft.
 *
 * @returns The wrapper.
 */
const mountFields = () =>
  mount(ServiceFilterFields, {
    props: { status: '', productId: '', billingCycle: '', products, testidPrefix: 'services-filter' },
    global: { plugins: [createTestI18n()] },
  })

/**
 * Finds one of the selects by the suffix of its test id.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `status`, `product` or `billing-cycle`.
 * @returns The select.
 */
const selectOf = (wrapper: ReturnType<typeof mountFields>, suffix: string) => {
  const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === `services-filter-${suffix}`)
  if (select === undefined) {
    throw new Error(`No select ends in ${suffix}`)
  }
  return select
}

/**
 * Reads the options one of the selects offers. UiSelect is generic, so its props are typed through a cast.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `status`, `product` or `billing-cycle`.
 * @returns The options the select offers.
 */
const optionsOf = (wrapper: ReturnType<typeof mountFields>, suffix: string): UiSelectOption<string>[] =>
  (selectOf(wrapper, suffix).props() as unknown as { options: UiSelectOption<string>[] }).options

describe('ServiceFilterFields', () => {
  it('offers any first, then the service statuses and billing cycles', () => {
    const wrapper = mountFields()

    expect(optionsOf(wrapper, 'status').map(option => option.value)).toEqual(['', 'Pending', 'Active', 'Suspended', 'Terminated'])
    expect(optionsOf(wrapper, 'billing-cycle').map(option => option.value))
      .toEqual(['', 'Monthly', 'Quarterly', 'SemiAnnual', 'Annual', 'Biennial'])
  })

  it('offers any first, then one choice per product', () => {
    const wrapper = mountFields()

    expect(optionsOf(wrapper, 'product')).toEqual([
      { value: '', label: 'Any' },
      { value: '1', label: 'Shared Starter' },
      { value: '2', label: 'VPS Small' },
    ])
  })

  it('writes the chosen values back to the draft', () => {
    const wrapper = mountFields()

    selectOf(wrapper, 'status').vm.$emit('update:modelValue', 'Active')
    selectOf(wrapper, 'product').vm.$emit('update:modelValue', '2')
    selectOf(wrapper, 'billing-cycle').vm.$emit('update:modelValue', 'Annual')

    expect(wrapper.emitted('update:status')).toEqual([['Active']])
    expect(wrapper.emitted('update:productId')).toEqual([['2']])
    expect(wrapper.emitted('update:billingCycle')).toEqual([['Annual']])
  })
})
