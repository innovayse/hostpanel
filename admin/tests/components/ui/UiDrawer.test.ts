/**
 * Unit tests for UiDrawer: shown only when open, closing paths, edge and footer.
 */
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
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

describe('UiDrawer attributes and dismissal', () => {
  it('puts the attributes a caller passes onto the panel', () => {
    const wrapper = mount(UiDrawer, {
      props: { open: true, title: 'x' },
      attrs: { 'data-testid': 'filters-drawer' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    expect(wrapper.get('aside').attributes('data-testid')).toBe('filters-drawer')
  })

  it('cannot be dismissed while it is not dismissible: no close button, Escape and the backdrop do nothing', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', dismissible: false })

    expect(wrapper.find('[data-testid="drawer-close"]').exists()).toBe(false)
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('.fixed').trigger('click')

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('ignores a backdrop click when it does not close on the backdrop, and still closes from the button', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', closeOnBackdrop: false })

    await wrapper.get('.fixed').trigger('click')
    expect(wrapper.emitted('close')).toBeUndefined()

    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('keeps a pointer-down on its backdrop from reaching the page, so a surface opened from a floating panel leaves that panel open', async () => {
    const onDown = vi.fn()
    document.addEventListener('mousedown', onDown)
    const wrapper = mount(UiDrawer, {
      props: { open: true, title: 'x' },
      attachTo: document.body,
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    await wrapper.get('.fixed').trigger('mousedown')
    document.removeEventListener('mousedown', onDown)
    wrapper.unmount()

    expect(onDown).not.toHaveBeenCalled()
  })

})

describe('UiDrawer as a sheet', () => {
  beforeAll(() => {
    // jsdom has no pointer capture; the sheet takes it for the finger that drags.
    HTMLElement.prototype.setPointerCapture = vi.fn()
  })

  afterAll(() => {
    Reflect.deleteProperty(HTMLElement.prototype, 'setPointerCapture')
  })

  it('is an opaque elevated surface, sheet or not', () => {
    expect(mountDrawer({ open: true, title: 'x' }).get('aside').classes()).toContain('glass-strong')
    expect(mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet }).get('aside').classes()).toContain('glass-strong')
  })

  it('is a bottom sheet with the safe-area inset and rounded top corners, whatever the screen width', () => {
    const classes = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet }).get('aside').classes()

    expect(classes).toEqual(expect.arrayContaining(['bottom-0', 'rounded-t-panel', 'max-h-[85dvh]', 'pb-[env(safe-area-inset-bottom)]']))
    expect(classes).not.toContain('top-0')
    expect(classes).not.toContain('max-w-md')
  })

  it('is the side panel by default', () => {
    const classes = mountDrawer({ open: true, title: 'x' }).get('aside').classes()

    expect(classes).toContain('top-0')
    expect(classes).toContain('max-w-md')
    expect(classes).not.toContain('rounded-t-panel')
  })

  it('draws a grip only for a sheet', () => {
    expect(mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet }).find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(mountDrawer({ open: true, title: 'x' }).find('[data-testid="drawer-grip"]').exists()).toBe(false)
  })

  it('follows a drag down with the finger and closes past the distance', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet })
    const aside = wrapper.get('aside')
    Object.defineProperty(aside.element, 'offsetHeight', { value: 400 })

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'mouse' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 160 })
    expect((aside.element as HTMLElement).style.getPropertyValue('--sheet-drag')).toBe('60px')
    expect(aside.attributes('data-dragging')).toBeDefined()

    await aside.trigger('pointerup', { pointerId: 1, clientY: 260 })

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('springs back instead of closing when it may not be dismissed', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet, dismissible: false })
    const aside = wrapper.get('aside')
    Object.defineProperty(aside.element, 'offsetHeight', { value: 400 })

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'mouse' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 160 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 300 })

    expect(wrapper.emitted('close')).toBeUndefined()
    expect((aside.element as HTMLElement).style.getPropertyValue('--sheet-drag')).toBe('0px')
  })

  it('springs back when the browser takes the touch over', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet })
    const aside = wrapper.get('aside')

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'mouse' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 150 })
    await aside.trigger('pointercancel', { pointerId: 1 })

    expect(wrapper.emitted('close')).toBeUndefined()
    expect((aside.element as HTMLElement).style.getPropertyValue('--sheet-drag')).toBe('0px')
    expect(aside.attributes('data-dragging')).toBeUndefined()
  })

  it('opens to full height when dragged up from a partial height', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet })
    const aside = wrapper.get('aside')

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 300, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 200 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 200 })

    expect(aside.classes()).toEqual(expect.arrayContaining(['h-[95dvh]', 'max-h-[95dvh]']))
    expect(aside.classes()).not.toContain('max-h-[85dvh]')
  })

  it('leaves a drag that starts in scrolled content to the content', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet })
    const aside = wrapper.get('aside')
    const body = wrapper.get('[data-testid="drawer-body"]')
    Object.defineProperty(body.element, 'scrollTop', { value: 40 })
    Object.defineProperty(aside.element, 'offsetHeight', { value: 400 })

    await body.trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 300 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 300 })

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('does not drag a side panel', async () => {
    const wrapper = mountDrawer({ open: true, title: 'x' })
    const aside = wrapper.get('aside')

    await aside.trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 300 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 400 })

    expect(wrapper.emitted('close')).toBeUndefined()
  })

describe('UiDrawer as a full-screen sheet', () => {
  it('fills the viewport with room for the notch and the home indicator, without rounded corners or a partial height', () => {
    const classes = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet, fullscreen: true }).get('aside').classes()

    expect(classes).toEqual(expect.arrayContaining(['inset-0', 'h-dvh', 'max-h-dvh', 'pt-[env(safe-area-inset-top)]', 'pb-[env(safe-area-inset-bottom)]']))
    expect(classes).not.toContain('rounded-t-panel')
    expect(classes).not.toContain('max-h-[85dvh]')
  })

  it('keeps the body scrolling and the footer pinned', () => {
    const wrapper = mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet, fullscreen: true }, { footer: '<b />' })

    expect(wrapper.get('[data-testid="drawer-body"]').classes()).toEqual(expect.arrayContaining(['overflow-y-auto', 'min-h-0']))
    expect(wrapper.get('[data-testid="ui-drawer-footer"]').classes()).toContain('shrink-0')
  })

  it('leaves a plain sheet a bottom sheet', () => {
    expect(mountDrawer({ open: true, title: 'x', presentation: UiDrawerPresentation.Sheet }).get('aside').classes()).toContain('rounded-t-panel')
  })
})
})
