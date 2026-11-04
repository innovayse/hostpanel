/**
 * Unit tests for the modal surface behaviour: Escape closes, focus moves in on open and back to
 * the opener on close. Mounted through a real component, as the dialog and drawer use it.
 */
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import type { Ref } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import { useModalSurface } from '@/utils/ui/modalSurface'

/** The `focusIn` of the surface mounted last, which a test calls as the enter animation would at its end. */
let focusSurface: () => void = () => undefined

/**
 * Builds a surface component whose open state is controlled by the test.
 *
 * @param open - Reactive open flag.
 * @param onClose - Called when the surface asks to close.
 * @returns The component.
 */
const surface = (open: Ref<boolean>, onClose: () => void) =>
  defineComponent({
    setup: () => {
      /** The surface element handed to the behaviour. */
      const panel = ref<HTMLElement | null>(null)
      const { onKeydown, focusIn } = useModalSurface(panel, () => open.value, onClose)
      focusSurface = focusIn
      return () => h('div', { ref: panel, id: 'panel', tabindex: -1, onKeydown }, [h('button', { id: 'inside' }, 'inside')])
    },
  })

afterEach(() => {
  document.body.innerHTML = ''
})

describe('useModalSurface', () => {
  it('calls close on Escape', () => {
    let closed = 0
    const wrapper = mount(
      surface(ref(false), () => {
        closed += 1
      }),
      { attachTo: document.body },
    )
    wrapper.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
    expect(closed).toBe(1)
  })

  it('does not close on other keys', () => {
    let closed = 0
    const wrapper = mount(
      surface(ref(false), () => {
        closed += 1
      }),
      { attachTo: document.body },
    )
    wrapper.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'a', bubbles: true }))
    expect(closed).toBe(0)
  })

  it('moves focus into the panel when it opens and back to the opener when it closes', async () => {
    const opener = document.createElement('button')
    document.body.append(opener)
    opener.focus()
    /** Open flag the test flips to show and hide the surface. */
    const open = ref(false)
    mount(
      surface(open, () => undefined),
      { attachTo: document.body },
    )
    open.value = true
    await nextTick()
    await nextTick()
    // At once only the panel itself holds focus, so Escape and Tab already work while it appears.
    expect(document.activeElement?.id).toBe('panel')
    focusSurface()
    expect(document.activeElement?.id).toBe('inside')
    open.value = false
    await nextTick()
    expect(document.activeElement).toBe(opener)
  })

  it('takes focus on mount when it starts open, and moves it to the first control at the end of the enter', async () => {
    mount(
      surface(ref(true), () => undefined),
      { attachTo: document.body },
    )
    await nextTick()
    await nextTick()
    expect(document.activeElement?.id).toBe('panel')
    focusSurface()
    expect(document.activeElement?.id).toBe('inside')
  })

  it('does not move focus to the first control once the surface has closed', async () => {
    /** Open flag the test flips. */
    const open = ref(true)
    mount(
      surface(open, () => undefined),
      { attachTo: document.body },
    )
    await nextTick()
    open.value = false
    await nextTick()
    focusSurface()
    expect(document.activeElement?.id).not.toBe('inside')
  })

  it('closes only the topmost of nested surfaces, whichever one the keystroke reached', async () => {
    const closed: string[] = []
    const outer = mount(
      surface(ref(true), () => closed.push('outer')),
      { attachTo: document.body },
    )
    await nextTick()
    const inner = mount(
      surface(ref(true), () => closed.push('inner')),
      { attachTo: document.body },
    )
    await nextTick()

    outer.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
    inner.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
    expect(closed).toEqual(['inner', 'inner'])

    inner.unmount()
    outer.element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }))
    expect(closed).toEqual(['inner', 'inner', 'outer'])
    outer.unmount()
  })
})
