/**
 * Mount tests for UiMenu: the trigger slot and its accessible label, the default kebab, the open panel and
 * selecting an action.
 */
import { describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiMenu from '@/components/ui/UiMenu.vue'
import { FLOATING_PANEL_CLASSES } from '@/utils/ui/floating'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the menu with two actions, optional props and an optional trigger slot.
 *
 * @param props - Props that override the defaults.
 * @param slots - Slot markup by slot name.
 * @returns The wrapper.
 */
const mountIt = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) =>
  mount(UiMenu, {
    props: { actions: [{ label: 'Edit', testid: 'action-edit', onSelect: vi.fn() }, { label: 'Delete', testid: 'action-delete', danger: true }], ...props },
    slots,
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    attachTo: document.body,
  })

describe('UiMenu', () => {
  it('shows a kebab trigger with the shared accessible label by default', () => {
    const wrapper = mountIt()
    const trigger = wrapper.get('[data-testid="ui-menu-trigger"]')
    expect(trigger.attributes('aria-label')).toBeTruthy()
    expect(trigger.find('svg').exists()).toBe(true)
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
    const wrapper = mountIt({ actions: [{ label: 'Edit', testid: 'action-edit', onSelect }, { label: 'Other' }] })
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(true)
    await wrapper.get('[data-testid="action-edit"]').trigger('click')
    expect(onSelect).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('draws a choice as a radio item and checks only the current one', async () => {
    const wrapper = mountIt({ actions: [{ label: 'One', testid: 'one', checked: true }, { label: 'Two', testid: 'two', checked: false }, { label: 'Plain', testid: 'plain' }] })
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')

    expect(wrapper.get('[data-testid="one"]').attributes('role')).toBe('menuitemradio')
    expect(wrapper.get('[data-testid="one"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="one"]').find('svg').exists()).toBe(true)
    expect(wrapper.get('[data-testid="two"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="two"]').find('svg').exists()).toBe(false)
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
    await wrapper.get('[data-testid="ui-menu-trigger"]').trigger('click')
    await nextTick()

    const panel = wrapper.get<HTMLElement>('[data-testid="ui-menu-panel"]')
    expect(panel.classes()).toEqual(expect.arrayContaining(['fixed', 'right-[var(--fp-right,auto)]', 'top-[var(--fp-top,auto)]']))
    expect(panel.element.style.getPropertyValue('--fp-right')).not.toBe('')
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
