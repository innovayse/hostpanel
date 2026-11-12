/**
 * Unit tests for UiChatAvatar: a decorative round mark with an optional tooltip.
 *
 * @module tests/components/ui/UiChatAvatar.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiChatAvatar from '~/components/ui/UiChatAvatar.vue'

describe('UiChatAvatar', () => {
  it('is hidden from assistive technology and round', () => {
    const wrapper = mountWithKit(UiChatAvatar)

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.classes()).toContain('rounded-full')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('gives the sender\'s name as the tooltip', () => {
    expect(mountWithKit(UiChatAvatar, { props: { label: 'Site assistant' } }).attributes('title')).toBe('Site assistant')
  })
})
