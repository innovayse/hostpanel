/**
 * Unit tests for UiDrawer: shown only when open, closing paths, edge and footer.
 */
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import { UiDrawerSheetBelow } from '@/utils/ui/drawer'
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

/**
 * Makes the screen narrow or not for the sheet query.
 *
 * @param narrow - Whether the sheet query matches.
 * @returns Nothing; `matchMedia` is stubbed.
 */
const stubNarrow = (narrow: boolean): void => {
  vi.stubGlobal('matchMedia', () => ({ matches: narrow, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
}

describe('UiDrawer as a sheet', () => {
  beforeAll(() => {
    // jsdom has no pointer capture; the sheet takes it for the finger that drags.
    HTMLElement.prototype.setPointerCapture = vi.fn()
  })

  afterAll(() => {
    Reflect.deleteProperty(HTMLElement.prototype, 'setPointerCapture')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is an opaque elevated surface, sheet or not', () => {
    expect(mountDrawer({ open: true, title: 'x' }).get('aside').classes()).toContain('glass-strong')
    expect(mountDrawer({ open: true, title: 'x', sheet: true }).get('aside').classes()).toContain('glass-strong')
  })

  it('is a bottom sheet on a narrow screen with the safe-area inset and rounded top corners', () => {
    stubNarrow(true)
    const classes = mountDrawer({ open: true, title: 'x', sheet: true }).get('aside').classes()

    expect(classes).toEqual(expect.arrayContaining(['bottom-0', 'rounded-t-panel', 'max-h-[85dvh]', 'pb-[env(safe-area-inset-bottom)]']))
    expect(classes).not.toContain('top-0')
    expect(classes).not.toContain('max-w-md')
  })

  it('is the side panel on a wide screen, and for a drawer that is not a sheet', () => {
    stubNarrow(false)
    const wide = mountDrawer({ open: true, title: 'x', sheet: true }).get('aside').classes()
    expect(wide).toContain('top-0')
    expect(wide).toContain('max-w-md')
    expect(wide).not.toContain('rounded-t-panel')

    stubNarrow(true)
    expect(mountDrawer({ open: true, title: 'x' }).get('aside').classes()).toContain('top-0')
  })

  it('asks the media query of the breakpoint it was given', () => {
    const queries: string[] = []
    vi.stubGlobal('matchMedia', (query: string) => {
      queries.push(query)
      return { matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }
    })

    mountDrawer({ open: true, title: 'x', sheet: true, sheetBelow: UiDrawerSheetBelow.Lg })

    expect(queries).toEqual(['(max-width: 1023.98px)'])
  })

  it('draws a grip only for a sheet that is a bottom sheet', () => {
    stubNarrow(true)
    expect(mountDrawer({ open: true, title: 'x', sheet: true }).find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(mountDrawer({ open: true, title: 'x' }).find('[data-testid="drawer-grip"]').exists()).toBe(false)
    stubNarrow(false)
    expect(mountDrawer({ open: true, title: 'x', sheet: true }).find('[data-testid="drawer-grip"]').exists()).toBe(false)
  })

  it('follows a drag down with the finger and closes past the distance', async () => {
    stubNarrow(true)
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    const aside = wrapper.get('aside')
    Object.defineProperty(aside.element, 'offsetHeight', { value: 400 })

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'mouse' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 160 })
    expect((aside.element as HTMLElement).style.getPropertyValue('--sheet-drag')).toBe('60px')
    expect(aside.attributes('data-dragging')).toBeDefined()

    await aside.trigger('pointerup', { pointerId: 1, clientY: 260 })

    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('springs back when the browser takes the touch over', async () => {
    stubNarrow(true)
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    const aside = wrapper.get('aside')

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'mouse' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 150 })
    await aside.trigger('pointercancel', { pointerId: 1 })

    expect(wrapper.emitted('close')).toBeUndefined()
    expect((aside.element as HTMLElement).style.getPropertyValue('--sheet-drag')).toBe('0px')
    expect(aside.attributes('data-dragging')).toBeUndefined()
  })

  it('opens to full height when dragged up from a partial height', async () => {
    stubNarrow(true)
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    const aside = wrapper.get('aside')

    await wrapper.get('[data-testid="drawer-grip"]').trigger('pointerdown', { pointerId: 1, clientY: 300, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 200 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 200 })

    expect(aside.classes()).toEqual(expect.arrayContaining(['h-[95dvh]', 'max-h-[95dvh]']))
    expect(aside.classes()).not.toContain('max-h-[85dvh]')
  })

  it('leaves a drag that starts in scrolled content to the content', async () => {
    stubNarrow(true)
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    const aside = wrapper.get('aside')
    const body = wrapper.get('[data-testid="drawer-body"]')
    Object.defineProperty(body.element, 'scrollTop', { value: 40 })
    Object.defineProperty(aside.element, 'offsetHeight', { value: 400 })

    await body.trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 300 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 300 })

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('follows a change of the breakpoint it was given', async () => {
    const queries: string[] = []
    vi.stubGlobal('matchMedia', (query: string) => {
      queries.push(query)
      return { matches: query === '(max-width: 1023.98px)', addEventListener: vi.fn(), removeEventListener: vi.fn() }
    })
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(false)

    await wrapper.setProps({ sheetBelow: UiDrawerSheetBelow.Lg })

    expect(queries).toContain('(max-width: 1023.98px)')
    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
  })

  it('does not drag a side panel', async () => {
    stubNarrow(false)
    const wrapper = mountDrawer({ open: true, title: 'x', sheet: true })
    const aside = wrapper.get('aside')

    await aside.trigger('pointerdown', { pointerId: 1, clientY: 100, button: 0, pointerType: 'touch' })
    await aside.trigger('pointermove', { pointerId: 1, clientY: 300 })
    await aside.trigger('pointerup', { pointerId: 1, clientY: 400 })

    expect(wrapper.emitted('close')).toBeUndefined()
  })
})
