/**
 * Unit tests for UiPopover: opening from the trigger slot, closing on Escape and on an outside
 * click, the panel class supplied by the screen, and the panel hanging from the trigger's edge.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiPopover from '~/components/ui/UiPopover.vue'
import { UiMenuAlign } from '~/utils/ui/enums'

/** Every popover mounted by the current test, unmounted afterwards so its document listeners go too. */
const mounted: VueWrapper[] = []

/**
 * Mounts a popover whose open state is bound the way a screen binds it.
 *
 * @param open Initial open state.
 * @param panelClass Width classes of the panel.
 * @param align Edge of the trigger the panel lines up with, when the test sets one.
 * @returns The wrapper of the popover.
 */
const mountPopover = (open: boolean, panelClass = '', align?: UiMenuAlign): VueWrapper => {
  const wrapper = mount(UiPopover, {
    props: {
      modelValue: open,
      panelClass,
      ...(align ? { align } : {}),
      'onUpdate:modelValue': (value: boolean) => wrapperProps.set(value),
    },
    slots: {
      trigger: ({ toggle }: { toggle: () => void }) => h('button', { 'data-testid': 'trigger', onClick: toggle }, 'Open'),
      default: () => h('span', { 'data-testid': 'body' }, 'Menu body'),
    },
    attachTo: document.body,
  })
  mounted.push(wrapper)
  return wrapper
}

/** Records the last value the popover asked its owner to hold. */
const wrapperProps = {
  last: undefined as boolean | undefined,
  /**
   * Stores the requested value.
   *
   * @param value The open state the popover emitted.
   */
  set(value: boolean) {
    this.last = value
  },
}

describe('UiPopover', () => {
  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  })

  it('renders no panel while closed', () => {
    const wrapper = mountPopover(false)

    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('renders the panel body as a menu while open, with the screen width classes', () => {
    const wrapper = mountPopover(true, 'w-72')
    const panel = wrapper.find('[data-testid="ui-popover-panel"]')

    expect(panel.attributes('role')).toBe('menu')
    expect(panel.classes()).toEqual(expect.arrayContaining(['w-72', 'rounded-2xl']))
    expect(panel.text()).toContain('Menu body')
  })

  it('hangs the panel under the trigger, right-aligned and capped to the viewport, by default', async () => {
    const wrapper = mountPopover(true, 'w-72')
    await nextTick()
    const panel = wrapper.find('[data-testid="ui-popover-panel"]')

    expect(wrapper.classes()).toContain('relative')
    expect(panel.classes()).toEqual(expect.arrayContaining(['absolute', 'right-0', 'top-full', 'max-w-[calc(100vw-2rem)]']))
    expect(panel.classes()).not.toContain('left-0')
    expect(panel.classes()).not.toContain('fixed')
  })

  it('lines the panel up with the left edge of the trigger when aligned to the start', async () => {
    const wrapper = mountPopover(true, 'w-72', UiMenuAlign.Start)
    await nextTick()
    const panel = wrapper.find('[data-testid="ui-popover-panel"]')

    expect(panel.classes()).toContain('left-0')
    expect(panel.classes()).not.toContain('right-0')
  })

  it('asks to open when the trigger toggles', async () => {
    wrapperProps.last = undefined
    const wrapper = mountPopover(false)

    await wrapper.find('[data-testid="trigger"]').trigger('click')

    expect(wrapperProps.last).toBe(true)
  })

  it('asks to close on Escape', () => {
    wrapperProps.last = undefined
    mountPopover(true)

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

    expect(wrapperProps.last).toBe(false)
  })

  it('asks to close on a click outside, but not on a click inside', async () => {
    wrapperProps.last = undefined
    const wrapper = mountPopover(true)

    await wrapper.find('[data-testid="body"]').trigger('click')
    expect(wrapperProps.last).toBeUndefined()

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    expect(wrapperProps.last).toBe(false)
  })
})
