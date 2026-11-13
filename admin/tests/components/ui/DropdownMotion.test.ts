/**
 * Every floating panel of the kit opens and closes through the shared dropdown transition (`UiTransition` of the
 * `dropdown` kind), so they all move the same way and none can forget reduced motion. Each case opens one panel
 * and reads the transition around it.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDateRangePicker from '@/components/ui/UiDateRangePicker.vue'
import UiFieldSelector from '@/components/ui/UiFieldSelector.vue'
import UiPhoneInput from '@/components/ui/UiPhoneInput.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'
import { createTestI18n } from '../../helpers/i18n'

/** The global options: the English catalogue and the teleport inlined. */
const global = { plugins: [createTestI18n()], stubs: { teleport: true } }

/** One panel: how to mount its component and which element opens it. */
interface Case {
  /** Name of the component. */
  name: string
  /** Mounts the component closed. */
  mountIt: () => VueWrapper
  /** CSS selector of the element whose click opens the panel. */
  opener: string
}

const cases: Case[] = [
  { name: 'UiSelect', mountIt: () => mount(UiSelect, { props: { modelValue: 'a', options: [{ value: 'a', label: 'A' }] }, global }), opener: 'button' },
  { name: 'UiDatePicker', mountIt: () => mount(UiDatePicker, { props: { modelValue: null }, global }), opener: 'input' },
  { name: 'UiDateRangePicker', mountIt: () => mount(UiDateRangePicker, { props: { modelValue: null }, global }), opener: 'input' },
  { name: 'UiClientSelect', mountIt: () => mount(UiClientSelect, { props: { modelValue: null, clients: [{ id: 1, name: 'Acme' }] }, global }), opener: 'button' },
  { name: 'UiPhoneInput', mountIt: () => mount(UiPhoneInput, { global }), opener: '[data-testid="ui-select-trigger"]' },
  { name: 'UiFieldSelector', mountIt: () => mount(UiFieldSelector, { props: { fields: [{ key: 'a', label: 'A' }], selected: new Set<string>() }, global }), opener: '[data-testid="field-selector-trigger"]' },
  { name: 'UiRowActions', mountIt: () => mount(UiRowActions, { props: { actions: [{ label: 'Edit', onSelect: () => undefined }, { label: 'View', onSelect: () => undefined }] }, global }), opener: '[data-testid="row-actions-trigger"]' },
]

/**
 * Lists every element under the wrapper's root, the root included.
 *
 * @param wrapper - The mounted component.
 * @returns The elements as they are now.
 */
const elementsOf = (wrapper: VueWrapper): Element[] => [wrapper.element as Element, ...wrapper.element.querySelectorAll('*')]

describe('floating panels use the shared dropdown transition', () => {
  it.each(cases)('$name', async ({ mountIt, opener }) => {
    const wrapper = mountIt()
    const transition = wrapper.findComponent(UiTransition)

    // Closed: the transition is already there, so the `v-if` that hides the panel is inside it.
    expect(transition.exists()).toBe(true)
    expect(transition.props('kind')).toBe(UiTransitionKind.Dropdown)

    const before = new Set(elementsOf(wrapper))
    await wrapper.get(opener).trigger('click')

    // Open: the one element that appeared directly inside the transition (the test utils render it as a stub
    // element, possibly under an inlined teleport) is the panel, so the panel is what the transition animates.
    const transitionElement = wrapper.findComponent(UiTransition).element
    const panels = elementsOf(wrapper).filter((element) => !before.has(element) && element.parentElement === transitionElement)
    expect(panels).toHaveLength(1)
    wrapper.unmount()
  })
})
