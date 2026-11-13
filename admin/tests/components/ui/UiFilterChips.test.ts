/**
 * Unit tests for UiFilterChips: one removable chip per active filter, a "Clear all" link from two chips up,
 * and nothing at all while no filter is active.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiFilterChips from '@/components/ui/UiFilterChips.vue'
import type { UiFilterChip } from '@/utils/ui/filter'
import { createTestI18n } from '../../helpers/i18n'

/** Two active filters. */
const chips: UiFilterChip[] = [
  { key: 'status', label: 'Status', value: 'Active' },
  { key: 'group', label: 'Group', value: 'VIP' },
]

/**
 * Mounts the chips with the English catalogue.
 *
 * @param list - The chips to show.
 * @returns The wrapper.
 */
const mountChips = (list: UiFilterChip[]) =>
  mount(UiFilterChips, { props: { chips: list }, global: { plugins: [createTestI18n()] } })

describe('UiFilterChips', () => {
  it('renders nothing without chips', () => {
    expect(mountChips([]).find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('shows the caption and the value of each chip in a labelled list', () => {
    const wrapper = mountChips(chips)

    expect(wrapper.get('ul').attributes('aria-label')).toBe('Active filters')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toBe('Status: Active')
    expect(wrapper.get('[data-testid="filter-chip-group"]').text()).toBe('Group: VIP')
  })

  it('names the remove button after its filter and reports the key', async () => {
    const wrapper = mountChips(chips)
    const button = wrapper.get('[data-testid="filter-chip-remove-group"]')

    expect(button.attributes('aria-label')).toBe('Remove filter Group: VIP')
    await button.trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['group']])
  })

  it('offers Clear all only from two chips up', async () => {
    expect(mountChips(chips.slice(0, 1)).find('[data-testid="filter-chips-clear"]').exists()).toBe(false)
    const wrapper = mountChips(chips)

    await wrapper.get('[data-testid="filter-chips-clear"]').trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })
})
