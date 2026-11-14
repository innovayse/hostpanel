/**
 * Helper for list screen tests: drives the filter dialog of `UiListToolbar` the way a reader does, open it, choose a
 * value in one of its selects, press Apply.
 */

import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'

/**
 * Opens the filter dialog, chooses a value in one of its selects and applies it.
 *
 * @param wrapper - The mounted list.
 * @param testid - The `data-testid` of the select to use.
 * @param value - The option value to choose.
 * @returns Resolves once the filter is applied and the list has settled.
 */
export const applyListFilter = async (wrapper: VueWrapper, testid: string, value: string): Promise<void> => {
  await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
  await flushPromises()
  wrapper
    .findAllComponents(UiSelect)
    .find((select) => select.attributes('data-testid') === testid)
    ?.vm.$emit('update:modelValue', value)
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}
