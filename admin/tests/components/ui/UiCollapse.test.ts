/**
 * Unit tests for UiCollapse: the content is rendered only while open, sits in a one-column grid whose rows the
 * shared collapse transition animates, and the transition is the collapse kind. While it closes the content is
 * inert, and a focus that was inside goes back to the toggle.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h, nextTick } from 'vue'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'

/**
 * Mounts the block around a paragraph.
 *
 * @param open - Whether the block is shown.
 * @returns The wrapper.
 */
const mountBlock = (open: boolean) => mount(UiCollapse, { props: { open }, slots: { default: '<p data-testid="content">Hello</p>' } })

describe('UiCollapse', () => {
  it('renders its content only while open, so a closed block takes no focus and is not read aloud', async () => {
    const wrapper = mountBlock(false)
    expect(wrapper.find('[data-testid="content"]').exists()).toBe(false)

    await wrapper.setProps({ open: true })

    expect(wrapper.get('[data-testid="content"]').text()).toBe('Hello')
  })

  it('animates the rows of a grid, never a measured height', () => {
    const wrapper = mountBlock(true)

    expect(wrapper.find('.grid').exists()).toBe(true)
    expect(wrapper.get('.grid > .min-h-0').find('[data-testid="content"]').exists()).toBe(true)
    expect(wrapper.findComponent(UiTransition).props('kind')).toBe(UiTransitionKind.Collapse)
  })

  describe('while it closes', () => {
    /** A toggle, a button outside, and a collapse holding a button. */
    const Host = defineComponent({
      props: { open: { type: Boolean, default: false } },
      setup: (props) => () =>
        h('div', [
          h('button', { 'data-testid': 'toggle' }, 'toggle'),
          h('button', { 'data-testid': 'outside' }, 'outside'),
          h(UiCollapse, { open: props.open }, { default: () => h('button', { 'data-testid': 'inner' }, 'inner') }),
        ]),
    })

    /** The mounted host, kept to unmount after each case. */
    let host: VueWrapper | null = null

    /**
     * Mounts the host closed and opens it from the toggle, the way a click on it would.
     *
     * @returns The wrapper, with the block open.
     */
    const openFromToggle = async (): Promise<VueWrapper> => {
      // The real transition is needed: the test utils stub it out by default, and the leave hook is the subject.
      host = mount(Host, { attachTo: document.body, global: { stubs: { transition: false } } })
      host.get<HTMLElement>('[data-testid="toggle"]').element.focus()
      await host.setProps({ open: true })
      return host
    }

    afterEach(() => {
      host?.unmount()
      host = null
    })

    it('makes the content inert for the leave', async () => {
      const wrapper = await openFromToggle()
      wrapper.get<HTMLElement>('[data-testid="inner"]').element.focus()

      await wrapper.setProps({ open: false })

      const inner = document.querySelector('[data-testid="inner"]')
      expect(inner?.closest('[inert]')).not.toBeNull()
    })

    it('gives the focus back to the toggle when it was inside the block', async () => {
      const wrapper = await openFromToggle()
      wrapper.get<HTMLElement>('[data-testid="inner"]').element.focus()

      await wrapper.setProps({ open: false })
      await nextTick()

      expect(document.activeElement).toBe(wrapper.get('[data-testid="toggle"]').element)
    })

    it('leaves the focus alone when it was elsewhere', async () => {
      const wrapper = await openFromToggle()
      wrapper.get<HTMLElement>('[data-testid="outside"]').element.focus()

      await wrapper.setProps({ open: false })

      expect(document.activeElement).toBe(wrapper.get('[data-testid="outside"]').element)
    })
  })
})
