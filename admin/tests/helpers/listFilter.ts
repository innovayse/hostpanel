/**
 * Helper for list screen tests: drives the filter dialog of `UiListToolbar` the way a reader does, open it, choose
 * values in its selects, press Apply. The selects themselves are found with `selectOf` from `select.ts`.
 */

import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { selectOf } from './select'

/** The test id of the dialog's trigger button unless the screen names its own (`filter-testid`). */
const DEFAULT_FILTER_TRIGGER = 'filter-trigger'

/**
 * Opens the filter dialog, chooses a value in each of the named selects and applies them together.
 *
 * @param wrapper - The mounted list.
 * @param chosen - The option value to choose per select, by the `data-testid` of the select.
 * @param triggerTestId - The `data-testid` of the button that opens the dialog; the toolbar's default when omitted.
 * @returns Resolves once the filters are applied and the list has settled.
 */
export const applyListFilter = async (
  wrapper: VueWrapper,
  chosen: Record<string, string>,
  triggerTestId: string = DEFAULT_FILTER_TRIGGER,
): Promise<void> => {
  await wrapper.get(`[data-testid="${triggerTestId}"]`).trigger('click')
  await flushPromises()
  for (const [testId, value] of Object.entries(chosen)) {
    selectOf(wrapper, testId).vm.$emit('update:modelValue', value)
  }
  await flushPromises()
  await wrapper.get('[data-testid="filter-apply"]').trigger('click')
  await flushPromises()
}
