/**
 * Unit tests for BillableItemTypeField: one select with "any" first and then the billable item types, the choice
 * written back to the draft, and the test id the screen gives it.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BillableItemTypeField from '@/components/billing/BillableItemTypeField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the field with an empty draft.
 *
 * @returns The wrapper.
 */
const mountField = () =>
  mount(BillableItemTypeField, {
    props: { modelValue: '', testid: 'items-filter-type' },
    global: { plugins: [createTestI18n()] },
  })

describe('BillableItemTypeField', () => {
  it('offers any first, then one time and recurring', () => {
    const select = mountField().getComponent(UiSelect)

    expect((select.props() as unknown as { options: UiSelectOption<string>[] }).options).toEqual([
      { value: '', label: 'Any' },
      { value: 'OneTime', label: 'One time' },
      { value: 'Recurring', label: 'Recurring' },
    ])
  })

  it('carries the test id it was given', () => {
    expect(mountField().getComponent(UiSelect).attributes('data-testid')).toBe('items-filter-type')
  })

  it('writes the chosen type back to the draft', () => {
    const wrapper = mountField()

    wrapper.getComponent(UiSelect).vm.$emit('update:modelValue', 'Recurring')

    expect(wrapper.emitted('update:modelValue')).toEqual([['Recurring']])
  })
})
