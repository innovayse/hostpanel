/**
 * Mount tests for UiMenu: the trigger slot and its accessible label, the default kebab, the open panel and
 * selecting an action.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiMenu from '@/components/ui/UiMenu.vue'
import { FLOATING_PANEL_CLASSES, FloatingAlign } from '@/utils/ui/floating'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'
import { Icon } from '@/lib/icons'

/**
 * Mounts the menu with two actions, optional props and an optional trigger slot.
 *
 * @param props - Props that override the defaults.
 * @param slots - Slot markup by slot name.
 * @returns The wrapper.
 */
const mountIt = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) =>
  mount(UiMenu, {
    props: { actions: [{ label: 'Edit', icon: Icon.Edit, testid: 'action-edit', onSelect: vi.fn() }, { label: 'Delete', icon: Icon.Trash, testid: 'action-delete', danger: true }], ...props },
    slots,
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    attachTo: document.body,
  })

describe('UiMenu', () => {
  it('shows a horizontal ellipsis trigger with the shared accessible label by default', () => {
    const wrapper = mountIt()
    const trigger = wrapper.get('[data-testid="ui-menu-trigger"]')
    expect(trigger.attributes('aria-label')).toBeTruthy()
    expect(trigger.find('svg').exists()).toBe(true)
    expect(trigger.findAll('svg circle').map(circle => circle.attributes('cy'))).toEqual(['12', '12', '12'])
    wrapper.unmount()
  })

  it('shows the trigger slot with the label the caller gave', () => {
    const wrapper = mountIt({ ariaLabel: 'Account menu', testid: 'who' }, { default: '<i data-testid="tile">A</i>' })
    const trigger = wrapper.get('[data-testid="who"]')
    expect(trigger.attributes('aria-label')).toBe('Account menu')
    expect(trigger.find('[data-testid="tile"]').exists()).toBe(true)
    expect(trigger.find('svg').exists()).toBe(false)
    wrapper.unmount()
  })

  it('draws the panel on the opaque floating surface, never glass', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    const classes = wrapper.get('[data-testid="ui-menu-panel"]').classes()
    expect(classes).toEqual(expect.arrayContaining(FLOATING_PANEL_CLASSES.split(' ')))
    expect(classes.some((name) => name.includes('glass'))).toBe(false)
    wrapper.unmount()
  })

  it('opens the panel, fires the picked action and closes', async () => {
    const onSelect = vi.fn()
    const wrapper = mountIt({ actions: [{ label: 'Edit', icon: Icon.Edit, testid: 'action-edit', onSelect }, { label: 'Other', icon: Icon.Check }] })
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(true)
    await wrapper.get('[data-testid="action-edit"]').trigger('click')
    expect(onSelect).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('draws a choice as a radio item and checks only the current one', async () => {
    const wrapper = mountIt({ actions: [{ label: 'One', icon: Icon.Check, testid: 'one', checked: true }, { label: 'Two', icon: Icon.Check, testid: 'two', checked: false }, { label: 'Plain', icon: Icon.Check, testid: 'plain' }] })
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    expect(wrapper.get('[data-testid="one"]').attributes('role')).toBe('menuitemradio')
    expect(wrapper.get('[data-testid="one"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="one"]').findAll('svg')).toHaveLength(2)
    expect(wrapper.get('[data-testid="two"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="two"]').findAll('svg')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plain"]').attributes('role')).toBe('menuitem')
    expect(wrapper.get('[data-testid="plain"]').attributes('aria-checked')).toBeUndefined()
    wrapper.unmount()
  })

  it('renders nothing without actions', () => {
    const wrapper = mountIt({ actions: [] })
    expect(wrapper.find('[data-testid="ui-menu-trigger"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('draws the panel fixed, placed through the shared floating variables', async () => {
    const wrapper = mountIt()
    const trigger = wrapper.get<HTMLElement>('[data-testid="ui-menu-trigger"]')
    // A trigger well inside the screen: the end aligned panel has room to grow leftwards.
    vi.spyOn(trigger.element, 'getBoundingClientRect').mockReturnValue({ left: 300, right: 340, top: 10, bottom: 50, width: 40, height: 40, x: 300, y: 10, toJSON: () => ({}) })
    await trigger.trigger('click')
    await nextTick()

    const panel = wrapper.get<HTMLElement>('[data-testid="ui-menu-panel"]')
    expect(panel.classes()).toEqual(expect.arrayContaining(['fixed', 'right-[var(--fp-right,auto)]', 'top-[var(--fp-top,auto)]']))
    expect(panel.element.style.getPropertyValue('--fp-right')).not.toBe('')
    wrapper.unmount()
  })

  it('hangs from the trigger start edge when asked to, as the account menu at the left of the screen does', async () => {
    const wrapper = mountIt({ align: FloatingAlign.Start })
    // jsdom has no layout: the viewport the panel must fit in is given a width.
    vi.spyOn(document.documentElement, 'clientWidth', 'get').mockReturnValue(1024)
    const trigger = wrapper.get<HTMLElement>('[data-testid="ui-menu-trigger"]')
    vi.spyOn(trigger.element, 'getBoundingClientRect').mockReturnValue({ left: 16, right: 52, top: 10, bottom: 50, width: 36, height: 40, x: 16, y: 10, toJSON: () => ({}) })
    await trigger.trigger('click')
    await nextTick()

    const panel = wrapper.get<HTMLElement>('[data-testid="ui-menu-panel"]')
    expect(panel.element.style.getPropertyValue('--fp-left')).toBe('16px')
    expect(panel.element.style.getPropertyValue('--fp-right')).toBe('')
    vi.restoreAllMocks()
    wrapper.unmount()
  })

  it('closes on Escape inside the menu and hands the focus back to the trigger', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    await wrapper.get('[data-testid="ui-menu-panel"]').trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('[data-testid="ui-menu-trigger"]').element)
    wrapper.unmount()
  })

  it('moves the focus between items with the arrow keys and wraps', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    await nextTick()
    expect(document.activeElement).toBe(wrapper.get('[data-testid="action-edit"]').element)

    await wrapper.get('[data-testid="ui-menu-panel"]').trigger('keydown', { key: 'ArrowDown' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="action-delete"]').element)

    await wrapper.get('[data-testid="ui-menu-panel"]').trigger('keydown', { key: 'ArrowDown' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="action-edit"]').element)
    wrapper.unmount()
  })

  it('closes on a pointer-down outside the trigger and the menu, not inside the menu', async () => {
    const wrapper = mountIt()
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    wrapper.get('[data-testid="ui-menu-panel"]').element.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(true)

    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await nextTick()
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })
})

describe('UiMenu on a phone', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens its list as a bottom sheet titled with the trigger label, keeping the menu roles and test ids', async () => {
    stubPhone(true)
    const wrapper = mountIt({ ariaLabel: 'Row actions' })

    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.get('h2').text()).toBe('Row actions')
    expect(wrapper.get('[data-testid="drawer-body"] [data-testid="ui-menu-panel"]').attributes('role')).toBe('menu')
    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(2)
    wrapper.unmount()
  })

  it('fires the picked action and closes the sheet, and closes on the sheet\'s close button', async () => {
    stubPhone(true)
    const onSelect = vi.fn()
    const wrapper = mountIt({ actions: [{ label: 'Edit', icon: Icon.Edit, testid: 'action-edit', onSelect }] })

    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    await wrapper.get('[data-testid="action-edit"]').trigger('click')
    expect(onSelect).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)

    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('stays open when the sheet takes the focus from the trigger, and closes on Escape and on its backdrop', async () => {
    stubPhone(true)
    const wrapper = mountIt()
    const trigger = wrapper.get<HTMLElement>('[data-testid="ui-menu-trigger"]')

    await trigger.trigger('click')
    // The drawer moves focus into its own aside as it opens: that focusout leaves the trigger for an element that is not the panel.
    const aside = wrapper.get<HTMLElement>('[data-testid="drawer"]').element
    trigger.element.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: aside }))
    await nextTick()

    expect(trigger.attributes('aria-expanded')).toBe('true')
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(true)

    await wrapper.get('[data-testid="ui-menu-panel"]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)

    await trigger.trigger('click')
    // The backdrop is the drawer's parent: a tap on it, not on the dialog, closes the sheet.
    wrapper.get<HTMLElement>('[data-testid="drawer"]').element.parentElement?.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    await nextTick()
    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('stays a popover on a wide screen', async () => {
    stubPhone(false)
    const wrapper = mountIt()

    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('makes the default trigger a 44px target on a phone', () => {
    const wrapper = mountIt()

    expect(wrapper.get('[data-testid="ui-menu-trigger"] span').classes()).toEqual(expect.arrayContaining(['size-8', 'max-sm:size-11']))
    wrapper.unmount()
  })
})
