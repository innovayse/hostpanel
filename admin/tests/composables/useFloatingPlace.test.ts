/**
 * Tests for `useFloatingPlace`: the panel gets its place when it opens and again when the page scrolls, but not
 * when its own list scrolls, and stops listening once it closes.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useFloatingPlace } from '@/composables/useFloatingPlace'
import { FloatingAlign } from '@/utils/ui/floating'

/** Whether the host's panel is open; the tests flip it. */
const open = ref(false)

/** Host with a trigger button and a panel that exists while open. Binds the composable's refs the way a kit component does. */
const Host = defineComponent({
  /**
   * Renders the trigger and, while open, the panel.
   *
   * @returns The render function.
   */
  setup() {
    /** The trigger button. */
    const trigger = ref<HTMLElement | null>(null)
    /** The panel, present while open. */
    const panel = ref<HTMLElement | null>(null)
    useFloatingPlace(trigger, panel, open, { align: FloatingAlign.Start, minRoom: 0, gap: 4 })
    return () => h('div', [
      h('button', { ref: trigger }),
      open.value ? h('div', { ref: panel, 'data-testid': 'panel' }, [h('ul', { 'data-testid': 'list' })]) : null,
    ])
  },
})

describe('useFloatingPlace', () => {
  it('places the panel once it is rendered, follows page scrolls and ignores the panel\'s own list', async () => {
    open.value = false
    const wrapper = mount(Host, { attachTo: document.body })

    open.value = true
    await nextTick()
    await nextTick()
    const panel = wrapper.get<HTMLElement>('[data-testid="panel"]').element
    expect(panel.style.getPropertyValue('--fp-left')).toBe('0px')

    // The panel's own list scrolling is not the page moving.
    panel.style.removeProperty('--fp-left')
    wrapper.get('[data-testid="list"]').element.dispatchEvent(new Event('scroll'))
    expect(panel.style.getPropertyValue('--fp-left')).toBe('')

    window.dispatchEvent(new Event('scroll'))
    expect(panel.style.getPropertyValue('--fp-left')).toBe('0px')
    wrapper.unmount()
  })

  it('stops following the page once the panel closes', async () => {
    open.value = false
    const wrapper = mount(Host, { attachTo: document.body })
    open.value = true
    await nextTick()
    await nextTick()
    const panel = wrapper.get<HTMLElement>('[data-testid="panel"]').element

    open.value = false
    await nextTick()
    panel.style.removeProperty('--fp-left')
    window.dispatchEvent(new Event('resize'))

    expect(panel.style.getPropertyValue('--fp-left')).toBe('')
    wrapper.unmount()
  })
})
