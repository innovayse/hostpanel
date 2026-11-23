/**
 * Unit tests for UiGradientText: the clip classes, the slot, the tag and caller gradient classes.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiGradientText from '~/components/ui/UiGradientText.vue'
import { UiGradientTextTag } from '~/utils/ui/enums'

describe('UiGradientText', () => {
  it('is a span clipped to its text with the slot inside', () => {
    const wrapper = mountWithKit(UiGradientText, { slots: { default: 'Price' } })

    expect(wrapper.element.tagName).toBe('SPAN')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-clip-text', 'text-transparent']))
    expect(wrapper.text()).toBe('Price')
  })

  it('renders the requested tag', () => {
    const wrapper = mountWithKit(UiGradientText, { props: { as: UiGradientTextTag.Div } })

    expect(wrapper.element.tagName).toBe('DIV')
  })

  it('keeps the gradient classes the caller adds', () => {
    const wrapper = mountWithKit(UiGradientText, { attrs: { class: 'bg-gradient-to-r from-cyan-400 to-primary-400' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-gradient-to-r', 'from-cyan-400', 'to-primary-400']))
  })
})
