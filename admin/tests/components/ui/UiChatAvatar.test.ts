/**
 * Unit tests for UiChatAvatar: a decorative round mark with an optional tooltip.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChatAvatar from '@/components/ui/UiChatAvatar.vue'

describe('UiChatAvatar', () => {
  it('is hidden from assistive technology and round', () => {
    const wrapper = mount(UiChatAvatar)

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.classes()).toContain('rounded-full')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('gives the sender\'s name as the tooltip', () => {
    expect(mount(UiChatAvatar, { props: { label: 'Site assistant' } }).attributes('title')).toBe('Site assistant')
  })
})
