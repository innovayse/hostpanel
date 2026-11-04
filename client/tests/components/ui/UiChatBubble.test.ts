/**
 * Unit tests for UiChatBubble: alignment and tint per side, the default side, and slot text shown as text.
 *
 * @module tests/components/ui/UiChatBubble.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiChatBubble from '~/components/ui/UiChatBubble.vue'
import { UiChatBubbleSide } from '~/utils/ui/enums'

describe('UiChatBubble', () => {
  it('aligns the reader\'s own message to the end with the primary tint', () => {
    const wrapper = mountWithKit(UiChatBubble, { props: { side: UiChatBubbleSide.Own }, slots: { default: 'Hi' } })

    expect(wrapper.classes()).toContain('justify-end')
    expect(wrapper.get('p').classes()).toContain('bg-primary-500/10')
    expect(wrapper.get('p').attributes('data-side')).toBe('own')
  })

  it('defaults to the other party, aligned to the start', () => {
    const wrapper = mountWithKit(UiChatBubble, { slots: { default: 'Hello' } })

    expect(wrapper.classes()).toContain('justify-start')
    expect(wrapper.get('p').attributes('data-side')).toBe('other')
  })

  it('keeps line breaks and shows slot text as text', () => {
    const wrapper = mountWithKit(UiChatBubble, { slots: { default: '{{ "<b>x</b>" }}' } })

    expect(wrapper.get('p').classes()).toContain('whitespace-pre-line')
    expect(wrapper.get('p').text()).toBe('<b>x</b>')
    expect(wrapper.find('b').exists()).toBe(false)
  })
})
