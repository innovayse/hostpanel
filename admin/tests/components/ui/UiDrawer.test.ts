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

  it('stacks the footer actions at full width on phones, right-aligned above', () => {
    const footer = mountDrawer({ open: true, title: 'x' }, { footer: '<b />' }).get('[data-testid="ui-drawer-footer"]')

    expect(footer.classes()).toEqual(expect.arrayContaining(['justify-end', 'max-sm:flex-col-reverse', 'max-sm:items-stretch']))
  })
})

describe('UiDrawer header row', () => {
  it('has the shell header height, so its hairline meets the top bar line', () => {
    const header = mountDrawer({ open: true, title: 'x' }).get('[data-testid="drawer-close"]').element.parentElement

    expect(header?.classList).toContain('h-(--shell-header-h)')
  })
})

describe('UiDrawer as a sheet', () => {
  it('is an opaque elevated surface, sheet or not', () => {
    expect(mountDrawer({ open: true, title: 'x' }).get('aside').classes()).toContain('glass-strong')
    expect(mountDrawer({ open: true, title: 'x', sheet: true }).get('aside').classes()).toContain('glass-strong')
  })

  it('is a bottom sheet on a phone with the safe-area inset and rounded top corners', () => {
    const classes = mountDrawer({ open: true, title: 'x', sheet: true }).get('aside').classes()

    expect(classes).toEqual(expect.arrayContaining(['bottom-0', 'rounded-t-panel', 'max-h-[85dvh]', 'max-sm:pb-[env(safe-area-inset-bottom)]']))
    expect(classes).toContain('sm:top-0')
    expect(classes).toContain('sm:border-l')
  })

  it('keeps the side panel for a drawer that is not a sheet', () => {
    const classes = mountDrawer({ open: true, title: 'x' }).get('aside').classes()

    expect(classes).toContain('top-0')
    expect(classes).not.toContain('rounded-t-panel')
  })

  it('moves the sheet limit and the edge to sm and up', () => {
    const left = mountDrawer({ open: true, title: 'x', sheet: true, side: 'left', narrow: true }).get('aside').classes()

    expect(left).toContain('sm:max-w-xs')
    expect(left).toContain('sm:border-r')
    expect(left).not.toContain('max-w-xs')
  })

  it('draws a grip only for a sheet', () => {
    expect(mountDrawer({ open: true, title: 'x', sheet: true }).find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(mountDrawer({ open: true, title: 'x' }).find('[data-testid="drawer-grip"]').exists()).toBe(false)
  })
})
