/**
 * Unit tests for SupportTicketFilterFields: the status, priority and department selects with an "any" choice first,
 * the department choices named after the support departments, and the chosen values written back to the draft.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SupportTicketFilterFields from '@/components/support/SupportTicketFilterFields.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/** The support departments the department field offers. */
const departments = [
  { id: 1, name: 'Support', email: 'support@example.test' },
  { id: 2, name: 'Billing', email: 'billing@example.test' },
]

/**
 * Mounts the fields with an empty draft.
 *
 * @returns The wrapper.
 */
const mountFields = () =>
  mount(SupportTicketFilterFields, {
    props: { status: '', priority: '', departmentId: '', departments, testidPrefix: 'tickets-filter' },
    global: { plugins: [createTestI18n()] },
  })

/**
 * Finds one of the selects by the suffix of its test id.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `status`, `priority` or `department`.
 * @returns The select.
 */
const selectOf = (wrapper: ReturnType<typeof mountFields>, suffix: string) =>
  wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === `tickets-filter-${suffix}`)

/**
 * Reads the options one of the selects offers. UiSelect is generic, so its props are typed through a cast.
 *
 * @param wrapper - The mounted fields.
 * @param suffix - `status`, `priority` or `department`.
 * @returns The options the select offers.
 */
const optionsOf = (wrapper: ReturnType<typeof mountFields>, suffix: string): UiSelectOption<string>[] => {
  const select = selectOf(wrapper, suffix)
  if (select === undefined) {
    throw new Error(`No select ends in ${suffix}`)
  }
  return (select.props() as unknown as { options: UiSelectOption<string>[] }).options
}

describe('SupportTicketFilterFields', () => {
  it('shows a status, a priority and a department select under the given test id prefix', () => {
    const wrapper = mountFields()

    expect(selectOf(wrapper, 'status')).toBeDefined()
    expect(selectOf(wrapper, 'priority')).toBeDefined()
    expect(selectOf(wrapper, 'department')).toBeDefined()
  })

  it('offers any first, then the ticket statuses and priorities', () => {
    const wrapper = mountFields()

    expect(optionsOf(wrapper, 'status').map(option => option.value)).toEqual(['', 'Open', 'AwaitingReply', 'Closed'])
    expect(optionsOf(wrapper, 'priority').map(option => option.value)).toEqual(['', 'Low', 'Medium', 'High'])
  })

  it('offers any first, then one choice per support department', () => {
    const wrapper = mountFields()

    expect(optionsOf(wrapper, 'department')).toEqual([
      { value: '', label: 'Any' },
      { value: '1', label: 'Support' },
      { value: '2', label: 'Billing' },
    ])
  })

  it('writes the chosen values back to the draft', async () => {
    const wrapper = mountFields()

    selectOf(wrapper, 'status')?.vm.$emit('update:modelValue', 'Closed')
    selectOf(wrapper, 'priority')?.vm.$emit('update:modelValue', 'High')
    selectOf(wrapper, 'department')?.vm.$emit('update:modelValue', '2')

    expect(wrapper.emitted('update:status')).toEqual([['Closed']])
    expect(wrapper.emitted('update:priority')).toEqual([['High']])
    expect(wrapper.emitted('update:departmentId')).toEqual([['2']])
  })
})
