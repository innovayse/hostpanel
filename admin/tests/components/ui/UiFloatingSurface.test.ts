/**
 * Unit tests for UiFloatingSurface: a popover with the caller's attributes and the floating place on a wide screen, a
 * bottom sheet (`UiDrawer`) on a phone, the content in both, and the panel model the owner's outside-click test reads.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiFloatingSurface from '@/components/ui/UiFloatingSurface.vue'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { FLOATING_PANEL_CLASSES, FLOATING_PLACE_CLASSES } from '@/utils/ui/floating'
import { createTestI18n } from '../../helpers/i18n'
import { stubPhone } from '../../helpers/phone'

/**
 * Mounts the surface with the teleports inlined and a marked content.
 *
 * @param props - Props that override the defaults.
 * @param attrs - Attributes the caller puts on the surface.
 * @returns The wrapper.
 */
const mountSurface = (props: Record<string, unknown> = {}, attrs: Record<string, unknown> = {}) =>
  mount(UiFloatingSurface, {
    props: { open: true, anchor: null, title: 'Actions', minRoom: 100, gap: 4, popoverClass: 'min-w-[12rem]', ...props },
    attrs: { 'data-testid': 'surface-content', role: 'menu', ...attrs },
    slots: { default: '<button data-testid="item">Edit</button>' },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('UiFloatingSurface as a popover', () => {
  it('draws the content on the opaque floating surface at its place, with the caller shape and attributes', () => {
    stubPhone(false)
    const wrapper = mountSurface()
    const panel = wrapper.get('[data-testid="surface-content"]')

    expect(panel.attributes('role')).toBe('menu')
    expect(panel.classes()).toEqual(expect.arrayContaining([...FLOATING_PLACE_CLASSES.split(' '), ...FLOATING_PANEL_CLASSES.split(' '), 'z-50', 'min-w-[12rem]']))
    expect(wrapper.findComponent(UiDrawer).exists()).toBe(false)
    expect(wrapper.find('[data-testid="item"]').exists()).toBe(true)
  })

  it('renders nothing while closed', () => {
    stubPhone(false)

    expect(mountSurface({ open: false }).find('[data-testid="surface-content"]').exists()).toBe(false)
  })

  it('hands the slot sheet=false and reports the popover element as the panel', async () => {
    stubPhone(false)
    let bound: HTMLElement | null = null
    const wrapper = mount(UiFloatingSurface, {
      props: { open: true, anchor: null, title: 'x', minRoom: 100, gap: 4, onPanel: (value: HTMLElement | null) => { bound = value } },
      slots: { default: '<template #default="{ sheet }"><i data-testid="mode">{{ sheet }}</i></template>' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="mode"]').text()).toBe('false')
    expect(bound).toBe(wrapper.get('div').element)
  })
})

describe('UiFloatingSurface as a bottom sheet', () => {
  it('is a sheet drawer titled by the caller on a phone, with the content and attributes inside it', () => {
    stubPhone(true)
    const wrapper = mountSurface()
    const drawer = wrapper.findComponent(UiDrawer)

    expect(drawer.props('presentation')).toBe(UiDrawerPresentation.Sheet)
    expect(wrapper.get('h2').text()).toBe('Actions')
    expect(wrapper.find('[data-testid="drawer-grip"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="drawer-body"] [data-testid="surface-content"]').attributes('role')).toBe('menu')
    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="item"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="surface-content"]').classes()).not.toContain('z-50')
  })

  it('asks its owner to close from Escape, the backdrop and the close button', async () => {
    stubPhone(true)
    const wrapper = mountSurface()

    await wrapper.get('[data-testid="drawer-close"]').trigger('click')
    await wrapper.get('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    await wrapper.get('.fixed').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(3)
  })

  it('reports the content wrapper inside the sheet as the panel, as the popover does', async () => {
    stubPhone(true)
    let bound: HTMLElement | null = null
    const wrapper = mount(UiFloatingSurface, {
      props: { open: true, anchor: null, title: 'x', minRoom: 100, gap: 4, onPanel: (value: HTMLElement | null) => { bound = value } },
      slots: { default: '<template #default="{ sheet }"><i data-testid="mode">{{ sheet }}</i></template>' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="mode"]').text()).toBe('true')
    expect(bound).toBe(wrapper.get('[data-testid="drawer-body"] > div').element)
  })

describe('UiFloatingSurface height', () => {
  it('keeps a tall popover inside the screen: it is as tall as the free room at most and scrolls inside itself', () => {
    stubPhone(false)
    const panel = mountSurface().get('[data-testid="surface-content"]')

    expect(panel.classes()).toEqual(expect.arrayContaining(['max-h-[var(--fp-max-h,none)]', 'overflow-y-auto']))
  })
})
})
