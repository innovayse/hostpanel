/**
 * Tests for `useDropdown`: the open flag, outside pointer-down, Escape with focus restore.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { useDropdown } from '@/composables/useDropdown'

/** Host that binds the composable's refs the way a component does. */
const Host = defineComponent({
  /**
   * Renders a root with a trigger button and a marker showing the open flag.
   *
   * @returns The render function.
   */
  setup() {
    const dropdown = useDropdown()
    return () => h('div', { ref: dropdown.root }, [
      h('button', { ref: dropdown.trigger, onClick: dropdown.toggle, onKeydown: dropdown.onEscape }),
      h('span', { 'data-open': String(dropdown.open.value) }),
    ])
  },
})

describe('useDropdown', () => {
  it('opens and closes on toggle', async () => {
    const wrapper = mount(Host, { attachTo: document.body })

    await wrapper.get('button').trigger('click')
    expect(wrapper.get('span').attributes('data-open')).toBe('true')
    await wrapper.get('button').trigger('click')
    expect(wrapper.get('span').attributes('data-open')).toBe('false')
    wrapper.unmount()
  })

  it('closes when the pointer goes down outside, not inside', async () => {
    const wrapper = mount(Host, { attachTo: document.body })
    await wrapper.get('button').trigger('click')

    wrapper.get('span').element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(wrapper.get('span').attributes('data-open')).toBe('true')

    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(wrapper.get('span').attributes('data-open')).toBe('false')
    wrapper.unmount()
  })

  it('closes on Escape, swallows the key and hands the focus to the trigger', async () => {
    const wrapper = mount(Host, { attachTo: document.body })
    await wrapper.get('button').trigger('click')
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true })

    wrapper.get('button').element.dispatchEvent(event)
    await nextTick()

    expect(wrapper.get('span').attributes('data-open')).toBe('false')
    expect(event.defaultPrevented).toBe(true)
    expect(document.activeElement).toBe(wrapper.get('button').element)
    wrapper.unmount()
  })

  it('ignores Escape while closed, so an enclosing dialog still sees it', () => {
    const wrapper = mount(Host, { attachTo: document.body })
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true })

    wrapper.get('button').element.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(false)
    wrapper.unmount()
  })
})
