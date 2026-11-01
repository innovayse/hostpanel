/**
 * Unit tests for UiPanelTransition: the shared open/close animation of dropdown panels.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'

describe('UiPanelTransition', () => {
  it('renders the wrapped panel', () => {
    const wrapper = mount(UiPanelTransition, {
      slots: { default: () => h('div', { 'data-testid': 'panel' }, 'Panel') },
    })

    expect(wrapper.get('[data-testid="panel"]').text()).toBe('Panel')
  })

  it('animates with a fade and respects reduced motion', () => {
    const wrapper = mount(UiPanelTransition, {
      slots: { default: () => h('div') },
    })
    const transition = wrapper.findComponent({ name: 'Transition' })

    expect(transition.exists()).toBe(true)
    expect(transition.props('enterActiveClass')).toContain('duration-300')
    expect(transition.props('leaveActiveClass')).toContain('duration-300')
    expect(transition.props('enterActiveClass')).toContain('motion-reduce:transition-none')
    expect(transition.props('leaveActiveClass')).toContain('motion-reduce:transition-none')
  })
})
