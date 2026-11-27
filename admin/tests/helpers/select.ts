/**
 * Helpers for screen tests that drive the kit's themed select (a button that opens a listbox). There are two ways in,
 * and each has one home here: a reader's way, open it and press the option by its visible label (`pickOption`), and the
 * component's way, find the select by its test id and send it the value (`selectOf`, which the filter dialog helper in
 * `listFilter.ts` builds on); `optionValues` and `selectText` read what it offers and shows.
 */

import type { DOMWrapper, VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'

/**
 * Opens the select behind a test id and picks the option with the given visible label. The label is matched on the
 * option's label line alone, so an option that also draws a caption or a description line is found by its label.
 *
 * @param wrapper - The mounted screen.
 * @param testId - The `data-testid` placed on the select.
 * @param label - Visible label of the option to pick.
 * @returns Resolves once the pick has been dispatched.
 */
export const pickOption = async (wrapper: VueWrapper, testId: string, label: string): Promise<void> => {
  await wrapper.get(`[data-testid="${testId}"] button`).trigger('click')
  const options: DOMWrapper<Element>[] = wrapper.findAll(`[data-testid="${testId}"] [role="option"]`)
  const option = options.find((candidate) => candidate.get('[data-testid="ui-select-option-label"]').text() === label)
  if (!option) {
    throw new Error(`No option "${label}" in ${testId}`)
  }
  await option.trigger('mousedown')
}

/**
 * The text a select currently shows on its trigger.
 *
 * @param wrapper - The mounted screen.
 * @param testId - The `data-testid` placed on the select.
 * @returns The trigger text.
 */
export const selectText = (wrapper: VueWrapper, testId: string): string =>
  wrapper.get(`[data-testid="${testId}"] button`).text()

/**
 * Finds the kit select that carries a test id.
 *
 * @param wrapper - The mounted screen.
 * @param testId - The `data-testid` placed on the select.
 * @returns The select's wrapper, whose `vm.$emit('update:modelValue', value)` chooses a value.
 */
export const selectOf = (wrapper: VueWrapper, testId: string) => {
  const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === testId)
  if (select === undefined) {
    throw new Error(`The select ${testId} is not mounted.`)
  }
  return select
}

/**
 * Reads the option values a mounted select offers, through its typed props because the select is generic.
 *
 * @param wrapper - The mounted screen.
 * @param testId - The `data-testid` placed on the select.
 * @returns The option values, in order.
 */
export const optionValues = (wrapper: VueWrapper, testId: string): string[] =>
  (selectOf(wrapper, testId).props() as unknown as { options: { value: string }[] }).options.map((option) => option.value)
