/**
 * Unit tests for UiScrollArea: the slot, the focusable scrolling element, the attributes that fall
 * through and the element handed to the parent through `v-model:element`.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiScrollArea from '~/components/ui/UiScrollArea.vue'

describe('UiScrollArea', () => {
  it('renders its slot in a focusable, vertically scrolling element', () => {
    const wrapper = mountWithKit(UiScrollArea, { slots: { default: 'Messages' } })

    expect(wrapper.text()).toBe('Messages')
    expect(wrapper.attributes('tabindex')).toBe('0')
    expect(wrapper.classes()).toContain('overflow-y-auto')
  })

  it('lets ARIA attributes and the test id through to the element', () => {
    const wrapper = mountWithKit(UiScrollArea, { attrs: { role: 'log', 'aria-live': 'polite', 'data-testid': 'log' } })

    expect(wrapper.attributes('role')).toBe('log')
    expect(wrapper.attributes('aria-live')).toBe('polite')
    expect(wrapper.attributes('data-testid')).toBe('log')
  })

  it('hands its element to the parent once mounted and takes it back on unmount', () => {
    const updates: Array<HTMLElement | null> = []
    const wrapper = mountWithKit(UiScrollArea, {
      attachTo: document.body,
      props: { element: null, 'onUpdate:element': (value: HTMLElement | null) => updates.push(value) },
    })

    expect(updates[0]).toBe(wrapper.element)

    wrapper.unmount()

    expect(updates[updates.length - 1]).toBeNull()
  })
})
