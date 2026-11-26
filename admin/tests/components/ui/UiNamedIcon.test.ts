/**
 * Unit tests for UiNamedIcon: the name is resolved in the Material Design set.
 */
import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiNamedIcon from '@/components/ui/UiNamedIcon.vue'

vi.mock('@iconify/vue', async () => {
  const { defineComponent, h } = await import('vue')
  return { Icon: defineComponent({ name: 'IconStub', props: ['icon'], setup: (props) => () => h('i', { 'data-icon': props.icon }) }) }
})

describe('UiNamedIcon', () => {
  it('draws the icon of the named Material Design entry', () => {
    const wrapper = mount(UiNamedIcon, { props: { name: 'cloud-check' } })

    expect(wrapper.attributes('data-icon')).toBe('mdi:cloud-check')
  })

  it('is hidden from assistive technology', () => {
    const wrapper = mount(UiNamedIcon, { props: { name: 'star' } })

    expect(wrapper.attributes('aria-hidden')).toBe('true')
  })
})
