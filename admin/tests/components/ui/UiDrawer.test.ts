/**
 * Unit tests for UiDrawer: shown only when open, closing paths, edge and footer.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the drawer with the teleport inlined.
 *
 * @param props - Props for the drawer.
 * @param slots - Slots for the drawer.
 * @returns The wrapper.
 */
const mountDrawer = (props: InstanceType<typeof UiDrawer>['$props'], slots: Record<string, string> = {}) =>
  mount(UiDrawer, { props, slots, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

describe('UiDrawer', () => {
  it('renders a labelled modal panel when open', () => {
    const wrapper = mountDrawer({ open: true, title: 'Filters' }, { default: '<p data-testid="body" />' })

    expect(wrapper.get('[role="dialog"]').attributes('aria-modal')).toBe('true')
    expect(wrapper.get('h2').text()).toBe('Filters')
    expect(wrapper.find('[data-testid="body"]').exists()).toBe(true)
  })

  it('renders nothing when closed', () => {
    expect(mountDrawer({ open: false, title: 'Filters' }).find('[role="dialog"]').exists()).toBe(false)
  })

  it('closes from the button, Escape and the backdrop', async () => {
    const wrapper = mountDrawer({ open: true, title: 'Filters' })

    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('.fixed').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(3)
  })

  it('attaches to the chosen edge', () => {
    expect(mountDrawer({ open: true, title: 'x' }).get('aside').classes()).toContain('right-0')
    expect(mountDrawer({ open: true, title: 'x', side: 'left' }).get('aside').classes()).toContain('left-0')
  })

  it('pads its body unless it is flush, and is as wide as a form unless it is narrow', () => {
    const plain = mountDrawer({ open: true, title: 'x' })
    expect(plain.get('[data-testid="drawer-body"]').classes()).toContain('p-6')
    expect(plain.get('aside').classes()).toContain('max-w-md')

    const menu = mountDrawer({ open: true, title: 'x', flush: true, narrow: true })
    expect(menu.get('[data-testid="drawer-body"]').classes()).not.toContain('p-6')
    expect(menu.get('aside').classes()).toContain('max-w-xs')
    expect(menu.get('aside').classes()).not.toContain('max-w-md')
  })

  it('keeps the close button out of sight until the panel has finished sliding in', () => {
    // The test transition is a stub that never finishes, which is the state "still sliding".
    expect(mountDrawer({ open: true, title: 'x' }).get('[data-testid="drawer-close"]').classes()).toContain('invisible')
  })

  it('renders the footer slot only when given', () => {
    expect(mountDrawer({ open: true, title: 'x' }).find('.border-t').exists()).toBe(false)
    expect(mountDrawer({ open: true, title: 'x' }, { footer: '<b data-testid="f" />' }).find('[data-testid="f"]').exists()).toBe(true)
  })
})
