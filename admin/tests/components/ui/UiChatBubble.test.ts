/**
 * Unit tests for UiChatBubble: the reader's own message sits on the right, tinted; the other party's on
 * the left, framed; the message is drawn as text.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'

describe('UiChatBubble', () => {
  it('puts the reader\'s own message on the right', () => {
    const wrapper = mount(UiChatBubble, { props: { side: UiChatBubbleSide.Own }, slots: { default: 'Hello' } })

    expect(wrapper.text()).toBe('Hello')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['self-end', 'bg-primary-500/15']))
    expect(wrapper.attributes('data-side')).toBe('own')
  })

  it('puts the other party\'s message on the left by default', () => {
    const wrapper = mount(UiChatBubble, { slots: { default: '<b>raw</b>' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['self-start', 'border']))
    expect(wrapper.attributes('data-side')).toBe('other')
  })
})
