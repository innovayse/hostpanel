/**
 * Mount tests for UiRowActions: trigger opens the menu, items are rendered, an action fires and
 * closes the menu, danger items are styled red, Escape closes without firing, and an outside click
 * closes the menu.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import { createTestI18n } from '../../helpers/i18n'

const actions = [
  { label: 'Edit', testid: 'action-edit', onSelect: vi.fn() },
  { label: 'Delete', testid: 'action-delete', onSelect: vi.fn(), danger: true },
]

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

/** Mounts the component with the standard action list. */
const mountIt = () =>
  mount(UiRowActions, {
    props: { actions },
    global: { plugins: [createTestI18n()] },
    attachTo: document.body,
  })

describe('UiRowActions', () => {
  it('hides the menu by default', () => {
    const wrapper = mountIt()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('opens the menu when the trigger is clicked', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('renders all action labels', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    const text = wrapper.get('[data-testid="row-actions-menu"]').text()
    expect(text).toContain('Edit')
    expect(text).toContain('Delete')
    wrapper.unmount()
  })

  it('calls onSelect and closes the menu when an action is clicked', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="action-edit"]').trigger('click')
    expect(actions[0]!.onSelect).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('applies danger styling to destructive actions', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    const deleteBtn = wrapper.get('[data-testid="action-delete"]')
    expect(deleteBtn.classes().some((c) => c.includes('danger'))).toBe(true)
    wrapper.unmount()
  })

  it('closes the menu on Escape', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('closes the menu on outside click', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await document.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('draws the menu fixed to the screen, so a table that scrolls inside its card cannot clip it', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')

    const menu = wrapper.get('[data-testid="row-actions-menu"]')
    expect(menu.classes()).toContain('fixed')
    expect(menu.classes()).not.toContain('absolute')
    wrapper.unmount()
  })

  it('closes the menu when something scrolls, because a fixed menu would be left behind', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')

    window.dispatchEvent(new Event('scroll'))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('places the menu with CSS variables that classes read, not with style properties', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')

    const menu = wrapper.get('[data-testid="row-actions-menu"]')
    expect((menu.element as HTMLElement).style.getPropertyValue('--ra-right')).not.toBe('')
    expect(menu.classes()).toEqual(expect.arrayContaining(['right-[var(--ra-right)]', 'top-[var(--ra-top,auto)]', 'bottom-[var(--ra-bottom,auto)]']))
    wrapper.unmount()
  })

  describe('scroll and resize listeners', () => {
    it('attaches none before the menu is opened', () => {
      const add = vi.spyOn(window, 'addEventListener')
      const wrapper = mountIt()

      expect(add.mock.calls.filter(([type]) => type === 'scroll' || type === 'resize')).toHaveLength(0)
      wrapper.unmount()
    })

    it('attaches them while open and removes them when the menu closes', async () => {
      const add = vi.spyOn(window, 'addEventListener')
      const remove = vi.spyOn(window, 'removeEventListener')
      const wrapper = mountIt()
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()

      expect(add.mock.calls.filter(([type]) => type === 'scroll' || type === 'resize').map(([type]) => type)).toEqual(['scroll', 'resize'])

      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      expect(remove.mock.calls.filter(([type]) => type === 'scroll' || type === 'resize').map(([type]) => type)).toEqual(['scroll', 'resize'])
      wrapper.unmount()
    })

    it('removes them when unmounted while open', async () => {
      const remove = vi.spyOn(window, 'removeEventListener')
      const wrapper = mountIt()
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()
      remove.mockClear()

      wrapper.unmount()

      expect(remove.mock.calls.filter(([type]) => type === 'scroll' || type === 'resize').map(([type]) => type)).toEqual(['scroll', 'resize'])
    })

    it('closes the menu on resize', async () => {
      const wrapper = mountIt()
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()

      window.dispatchEvent(new Event('resize'))
      await nextTick()

      expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
      wrapper.unmount()
    })
  })

  describe('placement of the fixed menu', () => {
    /**
     * Opens the menu with the trigger drawn at a given box and the page at a given size.
     *
     * @param trigger - The trigger's bounding box (right, top, bottom).
     * @param page - The viewport sizes without scrollbars, and the inner sizes that include them.
     * @returns The menu's CSS variables and the wrapper.
     */
    const openAt = async (
      trigger: { right: number; top: number; bottom: number },
      page: { innerHeight: number; innerWidth: number; clientHeight: number; clientWidth: number },
    ) => {
      vi.stubGlobal('innerHeight', page.innerHeight)
      vi.stubGlobal('innerWidth', page.innerWidth)
      vi.spyOn(document.documentElement, 'clientWidth', 'get').mockReturnValue(page.clientWidth)
      vi.spyOn(document.documentElement, 'clientHeight', 'get').mockReturnValue(page.clientHeight)
      const wrapper = mountIt()
      vi.spyOn(wrapper.get('[data-testid="row-actions-trigger"]').element, 'getBoundingClientRect').mockReturnValue(
        { ...trigger, left: 0, width: 0, height: trigger.bottom - trigger.top, x: 0, y: trigger.top, toJSON: () => ({}) },
      )
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()
      const style = (wrapper.get('[data-testid="row-actions-menu"]').element as HTMLElement).style
      return {
        top: style.getPropertyValue('--ra-top'),
        right: style.getPropertyValue('--ra-right'),
        bottom: style.getPropertyValue('--ra-bottom'),
        wrapper,
      }
    }

    it('opens below the trigger, 4px under it, with its right edge on the trigger\x27s', async () => {
      const { top, right, bottom, wrapper } = await openAt({ right: 900, top: 100, bottom: 130 }, { innerHeight: 800, innerWidth: 1000, clientHeight: 800, clientWidth: 1000 })

      expect(top).toBe('134px')
      expect(right).toBe('100px')
      expect(bottom).toBe('')
      wrapper.unmount()
    })

    it('opens above the trigger when less than 160px is left below it', async () => {
      const { top, bottom, wrapper } = await openAt({ right: 900, top: 700, bottom: 730 }, { innerHeight: 800, innerWidth: 1000, clientHeight: 800, clientWidth: 1000 })

      expect(bottom).toBe('104px')
      expect(top).toBe('')
      wrapper.unmount()
    })

    it('measures the right edge from the width without the page scrollbar, not from innerWidth', async () => {
      const { right, wrapper } = await openAt({ right: 900, top: 100, bottom: 130 }, { innerHeight: 800, innerWidth: 1000, clientHeight: 800, clientWidth: 985 })

      expect(right).toBe('85px')
      wrapper.unmount()
    })

    it('measures the vertical room from clientHeight, not from innerHeight', async () => {
      // 800 inner but 780 usable: 780 - 630 = 150 < 160, so it opens above; innerHeight alone would say 170 and open below.
      const { top, bottom, wrapper } = await openAt({ right: 900, top: 600, bottom: 630 }, { innerHeight: 800, innerWidth: 1000, clientHeight: 780, clientWidth: 1000 })

      expect(bottom).toBe('184px')
      expect(top).toBe('')
      wrapper.unmount()
    })
  })
})
