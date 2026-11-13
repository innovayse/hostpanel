/**
 * Mount tests for UiRowActions (the teleport is inlined unless a test says otherwise): trigger opens the menu, items are rendered, an action fires and
 * closes the menu, danger items are styled red, Escape closes without firing, and an outside click
 * closes the menu. A per-row testid names the trigger, and an action with a route renders as a real link.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import { Icon } from '@/lib/icons'
import type { Router } from 'vue-router'
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
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
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
    expect(deleteBtn.classes()).toContain('text-status-red')
    wrapper.unmount()
  })

  it('closes the menu on Escape', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    wrapper.get('[data-testid="row-actions-menu"]').element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
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

  it('places the menu again when something scrolls, because a fixed menu would be left behind', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await nextTick()
    const menu = wrapper.get<HTMLElement>('[data-testid="row-actions-menu"]').element
    menu.style.removeProperty('--fp-right')

    window.dispatchEvent(new Event('scroll'))

    expect(menu.style.getPropertyValue('--fp-right')).not.toBe('')
    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('closes the menu when focus leaves it with Tab, without taking focus back', async () => {
    const outside = document.createElement('button')
    document.body.appendChild(outside)
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await nextTick()

    outside.focus()
    await nextTick()

    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
    expect(document.activeElement).toBe(outside)
    wrapper.unmount()
    outside.remove()
  })

  it('keeps the menu open while focus moves between its own items', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await nextTick()

    const second = wrapper.get('[data-testid="action-delete"]').element as HTMLElement
    second.focus()
    await nextTick()

    expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('places the menu with CSS variables that classes read, not with style properties', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')

    const menu = wrapper.get('[data-testid="row-actions-menu"]')
    expect((menu.element as HTMLElement).style.getPropertyValue('--fp-right')).not.toBe('')
    expect(menu.classes()).toEqual(expect.arrayContaining(['right-[var(--fp-right,auto)]', 'top-[var(--fp-top,auto)]', 'bottom-[var(--fp-bottom,auto)]']))
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

    it('places the menu again on resize and keeps it open', async () => {
      const wrapper = mountIt()
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()
      const menu = wrapper.get<HTMLElement>('[data-testid="row-actions-menu"]').element
      menu.style.removeProperty('--fp-right')

      window.dispatchEvent(new Event('resize'))

      expect(menu.style.getPropertyValue('--fp-right')).not.toBe('')
      expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
      wrapper.unmount()
    })
  })

  describe('menu drawn in the page body', () => {
    it('teleports the menu to the body and keeps it open while the pointer goes down inside it', async () => {
      const wrapper = mount(UiRowActions, {
        props: { actions },
        global: { plugins: [createTestI18n()] },
        attachTo: document.body,
      })
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await nextTick()

      const menu = document.body.querySelector('[data-testid="row-actions-menu"]')
      expect(menu).not.toBeNull()
      expect(wrapper.element.contains(menu)).toBe(false)

      menu?.querySelector('[data-testid="action-edit"]')?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
      await nextTick()
      expect(document.body.querySelector('[data-testid="row-actions-menu"]')).not.toBeNull()

      wrapper.unmount()
    })

    it('shows the icon of a menu action beside its label', async () => {
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Edit', testid: 'action-edit', icon: Icon.Edit, onSelect: vi.fn() }, ...actions.slice(1)] },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
        attachTo: document.body,
      })
      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')

      expect(wrapper.get('[data-testid="action-edit"]').find('svg').exists()).toBe(true)
      expect(wrapper.get('[data-testid="action-delete"]').find('svg').exists()).toBe(false)
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
        top: style.getPropertyValue('--fp-top'),
        right: style.getPropertyValue('--fp-right'),
        bottom: style.getPropertyValue('--fp-bottom'),
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

  describe('one action against many', () => {
    it('shows one action as its own labelled button with no menu trigger', async () => {
      const onSelect = vi.fn()
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Open', testid: 'action-open', onSelect }] },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
      })

      expect(wrapper.find('[data-testid="row-actions-trigger"]').exists()).toBe(false)
      const button = wrapper.get('[data-testid="action-open"]')
      expect(button.text()).toBe('Open')
      await button.trigger('click')
      expect(onSelect).toHaveBeenCalledOnce()
      wrapper.unmount()
    })

    it('draws a single action with an icon as an icon button named by its label', async () => {
      const onSelect = vi.fn()
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Open', testid: 'action-open', icon: Icon.Eye, onSelect }] },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
      })

      const button = wrapper.get('[data-testid="action-open"]')
      expect(button.find('svg').exists()).toBe(true)
      expect(button.attributes('title')).toBe('Open')
      expect(button.get('.sr-only').text()).toBe('Open')
      await button.trigger('click')
      expect(onSelect).toHaveBeenCalledOnce()
      wrapper.unmount()
    })

    it('does not fire a disabled single action', async () => {
      const onSelect = vi.fn()
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Open', testid: 'action-open', onSelect, disabled: true }] },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
      })

      await wrapper.get('[data-testid="action-open"]').trigger('click')
      expect(onSelect).not.toHaveBeenCalled()
      wrapper.unmount()
    })

    it('puts two or more actions behind one labelled kebab trigger', () => {
      const wrapper = mountIt()

      const trigger = wrapper.get('[data-testid="row-actions-trigger"]')
      expect(trigger.attributes('aria-label')).toBeTruthy()
      expect(trigger.attributes('aria-haspopup')).toBe('menu')
      expect(wrapper.find('[data-testid="action-edit"]').exists()).toBe(false)
      wrapper.unmount()
    })

    it('moves destructive actions to the end of the menu', async () => {
      const wrapper = mount(UiRowActions, {
        props: {
          actions: [
            { label: 'Delete', testid: 'first-delete', onSelect: vi.fn(), danger: true },
            { label: 'Edit', testid: 'then-edit', onSelect: vi.fn() },
            { label: 'View', testid: 'then-view', onSelect: vi.fn() },
          ],
        },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
        attachTo: document.body,
      })

      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      const ids = wrapper.findAll('[role="menuitem"]').map((item) => item.attributes('data-testid'))
      expect(ids).toEqual(['then-edit', 'then-view', 'first-delete'])
      wrapper.unmount()
    })

    it('renders nothing without actions', () => {
      const wrapper = mount(UiRowActions, { props: { actions: [] }, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

      expect(wrapper.find('[data-testid="row-actions"]').exists()).toBe(false)
      wrapper.unmount()
    })
  })

  describe('per-row trigger testid', () => {
    it('keeps the shared trigger testid when none is given', () => {
      const wrapper = mountIt()
      expect(wrapper.find('[data-testid="row-actions-trigger"]').exists()).toBe(true)
      wrapper.unmount()
    })

    it('names the trigger with the testid the row passes', async () => {
      const wrapper = mount(UiRowActions, {
        props: { actions, testid: 'invoice-actions-7' },
        global: { plugins: [createTestI18n()], stubs: { teleport: true } },
        attachTo: document.body,
      })

      expect(wrapper.find('[data-testid="row-actions-trigger"]').exists()).toBe(false)
      await wrapper.get('[data-testid="invoice-actions-7"]').trigger('click')
      expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(true)
      wrapper.unmount()
    })
  })

  describe('link actions', () => {
    /**
     * Builds a memory router with the pages the link actions point at.
     *
     * @returns The router, already on the start page.
     */
    const makeRouter = async (): Promise<Router> => {
      const router = createRouter({
        history: createMemoryHistory(),
        routes: [
          { path: '/', component: { template: '<div />' } },
          { path: '/items/:id', component: { template: '<div />' } },
        ],
      })
      await router.push('/')
      await router.isReady()
      return router
    }

    /** A link action and a destructive button action, the mix a billing row has. */
    const linkAndDelete = () => [
      { label: 'View', testid: 'action-view', to: '/items/4' },
      { label: 'Delete', testid: 'action-delete', onSelect: vi.fn(), danger: true },
    ]

    it('renders a menu action with a route as a menuitem anchor with its href', async () => {
      const router = await makeRouter()
      const wrapper = mount(UiRowActions, {
        props: { actions: linkAndDelete() },
        global: { plugins: [createTestI18n(), router], stubs: { teleport: true } },
        attachTo: document.body,
      })

      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      const link = wrapper.get('[data-testid="action-view"]')
      expect(link.element.tagName).toBe('A')
      expect(link.attributes('href')).toBe('/items/4')
      expect(link.attributes('role')).toBe('menuitem')
      expect(wrapper.get('[data-testid="action-delete"]').element.tagName).toBe('BUTTON')
      wrapper.unmount()
    })

    it('navigates and closes the menu when a link item is clicked', async () => {
      const router = await makeRouter()
      const wrapper = mount(UiRowActions, {
        props: { actions: linkAndDelete() },
        global: { plugins: [createTestI18n(), router], stubs: { teleport: true } },
        attachTo: document.body,
      })

      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await wrapper.get('[data-testid="action-view"]').trigger('click')
      await flushPromises()
      expect(router.currentRoute.value.fullPath).toBe('/items/4')
      expect(wrapper.find('[data-testid="row-actions-menu"]').exists()).toBe(false)
      wrapper.unmount()
    })

    it('moves the arrow-key focus onto link items too', async () => {
      const router = await makeRouter()
      const wrapper = mount(UiRowActions, {
        props: {
          actions: [
            { label: 'Edit', testid: 'action-edit', onSelect: vi.fn() },
            { label: 'View', testid: 'action-view', to: '/items/4' },
          ],
        },
        global: { plugins: [createTestI18n(), router], stubs: { teleport: true } },
        attachTo: document.body,
      })

      await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
      await flushPromises()
      document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }))
      expect(document.activeElement).toBe(wrapper.get('[data-testid="action-view"]').element)
      wrapper.unmount()
    })

    it('renders a single link action as an anchor, not a button', async () => {
      const router = await makeRouter()
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Open', testid: 'action-open', to: '/items/2' }] },
        global: { plugins: [createTestI18n(), router], stubs: { teleport: true } },
      })

      const link = wrapper.get('[data-testid="action-open"]')
      expect(link.element.tagName).toBe('A')
      expect(link.attributes('href')).toBe('/items/2')
      wrapper.unmount()
    })

    it('renders a disabled link action as a disabled button, so it cannot be followed', async () => {
      const router = await makeRouter()
      const wrapper = mount(UiRowActions, {
        props: { actions: [{ label: 'Open', testid: 'action-open', to: '/items/2', disabled: true }] },
        global: { plugins: [createTestI18n(), router], stubs: { teleport: true } },
      })

      const button = wrapper.get('[data-testid="action-open"]')
      expect(button.element.tagName).toBe('BUTTON')
      expect(button.attributes('disabled')).toBeDefined()
      wrapper.unmount()
    })
  })
})
