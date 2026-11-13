/**
 * Unit tests for UiChatBubble: the reader's own message sits on the right, tinted; the other party's on
 * the left, both glass; the message is drawn as text, under an optional sender and time line.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'

describe('UiChatBubble', () => {
  it('keeps its side and draws bars instead of the message while loading', () => {
    const wrapper = mount(UiChatBubble, { props: { side: UiChatBubbleSide.Own, loading: true }, slots: { default: 'Hello' } })

    expect(wrapper.attributes('data-side')).toBe('own')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="ui-chat-bubble-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toBe('')
  })

  it('keeps its side and draws bars instead of the message while loading', () => {
    const wrapper = mount(UiChatBubble, { props: { side: UiChatBubbleSide.Own, loading: true }, slots: { default: 'Hello' } })

    expect(wrapper.attributes('data-side')).toBe('own')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('[data-testid="ui-chat-bubble-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toBe('')
  })

  it('puts the reader\'s own message on the right', () => {
    const wrapper = mount(UiChatBubble, { props: { side: UiChatBubbleSide.Own }, slots: { default: 'Hello' } })

    expect(wrapper.text()).toBe('Hello')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['self-end', 'glass', 'bg-primary-500/15']))
    expect(wrapper.attributes('data-side')).toBe('own')
  })

  it('puts the other party\'s message on the left by default', () => {
    const wrapper = mount(UiChatBubble, { slots: { default: '<b>raw</b>' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['self-start', 'glass']))
    expect(wrapper.attributes('data-side')).toBe('other')
  })

  it('has no header line without a sender or a time', () => {
    const wrapper = mount(UiChatBubble, { slots: { default: 'Hello' } })

    expect(wrapper.find('[data-testid="ui-chat-bubble-head"]').exists()).toBe(false)
  })

  it('shows the sender and the time above the message', () => {
    const wrapper = mount(UiChatBubble, { props: { sender: 'Ada', time: '09:30' }, slots: { default: 'Hello' } })

    expect(wrapper.get('[data-testid="ui-chat-bubble-sender"]').text()).toBe('Ada')
    expect(wrapper.get('[data-testid="ui-chat-bubble-time"]').text()).toBe('09:30')
    expect(wrapper.get('[data-testid="ui-chat-bubble-body"]').text()).toBe('Hello')
  })

  it('lets the sender and time slots replace the props', () => {
    const wrapper = mount(UiChatBubble, {
      props: { sender: 'Ada' },
      slots: { default: 'Hello', sender: '<i data-testid="custom-sender">Staff</i>', time: '<b data-testid="custom-time">now</b>' },
    })

    expect(wrapper.find('[data-testid="ui-chat-bubble-sender"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="custom-sender"]').text()).toBe('Staff')
    expect(wrapper.get('[data-testid="custom-time"]').text()).toBe('now')
  })
})
