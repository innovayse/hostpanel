/**
 * Unit tests for UiBlock: a layout-free element that renders its slot, takes the chosen tag and
 * keeps the caller's class and attributes.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiBlock from '~/components/ui/UiBlock.vue'
import { UiBlockTag } from '~/utils/ui/enums'

describe('UiBlock', () => {
  it('renders its slot in a div without any class of its own', () => {
    const wrapper = mountWithKit(UiBlock, { slots: { default: 'inside' } })

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.text()).toBe('inside')
    expect(wrapper.classes()).toHaveLength(0)
  })

  it('renders the requested landmark and passes class and attributes through', () => {
    const wrapper = mountWithKit(UiBlock, { props: { as: UiBlockTag.Footer }, attrs: { class: 'p-4', 'data-testid': 'block' } })

    expect(wrapper.element.tagName).toBe('FOOTER')
    expect(wrapper.classes()).toContain('p-4')
    expect(wrapper.attributes('data-testid')).toBe('block')
  })
})
