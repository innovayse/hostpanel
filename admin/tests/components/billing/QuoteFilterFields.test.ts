/**
 * Unit tests for QuoteFilterFields: the stage and validity selects with an "any" choice first, every quote stage and
 * both validities after it, and the chosen values written back to the draft.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import QuoteFilterFields from '@/components/billing/QuoteFilterFields.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { QuoteStage, QuoteValidity } from '@/types/quote'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the fields with an empty draft.
 *
 * @returns The wrapper.
 */
const mountFields = () =>
  mount(QuoteFilterFields, {
    props: { stage: '', validity: '', testidPrefix: 'quotes-filter' },
    global: { plugins: [createTestI18n()] },
  })

/**
 * Finds one of the selects by the suffix of its test id.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `stage` or `validity`.
 * @returns The select.
 */
const selectOf = (wrapper: ReturnType<typeof mountFields>, suffix: string) =>
  wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === `quotes-filter-${suffix}`)

/**
 * Reads the options one of the selects offers. UiSelect is generic, so its props are typed through a cast.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `stage` or `validity`.
 * @returns The options the select offers.
 */
const optionsOf = (wrapper: ReturnType<typeof mountFields>, suffix: string): UiSelectOption<string>[] => {
  const select = selectOf(wrapper, suffix)
  if (select === undefined) {
    throw new Error(`No select ends in ${suffix}`)
  }
  return (select.props() as unknown as { options: UiSelectOption<string>[] }).options
}

describe('QuoteFilterFields', () => {
  it('shows a stage and a validity select under the given test id prefix', () => {
    const wrapper = mountFields()

    expect(selectOf(wrapper, 'stage')).toBeDefined()
    expect(selectOf(wrapper, 'validity')).toBeDefined()
  })

  it('offers any first, then every quote stage', () => {
    const options = optionsOf(mountFields(), 'stage')

    expect(options[0]).toEqual({ value: '', label: 'Any' })
    expect(options.slice(1).map((option) => option.value).sort()).toEqual(Object.values(QuoteStage).sort())
  })

  it('offers any first, then valid and expired', () => {
    const options = optionsOf(mountFields(), 'validity')

    expect(options.map((option) => option.value)).toEqual(['', QuoteValidity.Valid, QuoteValidity.Expired])
    expect(options[0]).toEqual({ value: '', label: 'Any' })
  })

  it('writes the chosen values back to the draft', () => {
    const wrapper = mountFields()

    selectOf(wrapper, 'stage')?.vm.$emit('update:modelValue', 'Lost')
    selectOf(wrapper, 'validity')?.vm.$emit('update:modelValue', 'Expired')

    expect(wrapper.emitted('update:stage')).toEqual([['Lost']])
    expect(wrapper.emitted('update:validity')).toEqual([['Expired']])
  })
})
