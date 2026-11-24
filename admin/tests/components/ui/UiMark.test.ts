/**
 * Unit tests for UiMark: the highlight element around the matched text.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiMark from '@/components/ui/UiMark.vue'

describe('UiMark', () => {
  it('draws its content in a mark element with the accent look', () => {
    const wrapper = mount(UiMark, { slots: { default: 'bill' } })

    expect(wrapper.element.tagName).toBe('MARK')
    expect(wrapper.text()).toBe('bill')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-transparent', 'font-semibold', 'text-text-accent']))
  })
})
