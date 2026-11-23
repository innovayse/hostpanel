/**
 * Unit tests for UiSidebar: entries as links with icons drawn without v-html, active marker and
 * the flyout that opens over the content on hover or keyboard focus.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiSidebar from '@/components/ui/UiSidebar.vue'

/** Two entries with one-shape icons. */
const items = [
  { key: 'payments', label: 'Payments', to: '/integrations/payments', icon: '<rect x="1" y="4" width="22" height="16"/>' },
  { key: 'email', label: 'Email', to: '/integrations/email', icon: '<circle cx="12" cy="12" r="10"/>' },
]

/**
 * Mounts the sidebar with the router link stubbed.
 *
 * @param activeKey - Key of the current entry.
 * @returns The wrapper.
 */
const mountSidebar = (activeKey?: string) =>
  mount(UiSidebar, {
    props: { items, activeKey, testid: 'int' },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })

/**
 * The panel of a mounted sidebar: the box that holds the entries and grows when it opens.
 *
 * @param wrapper - The mounted sidebar.
 * @returns Its classes.
 */
const panelClasses = (wrapper: ReturnType<typeof mountSidebar>): string[] => wrapper.get('[data-testid="int-panel"]').classes()

describe('UiSidebar', () => {
  // A spy on `Element.prototype.matches` also changes how the test utils find elements, so it must never outlive its test.
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders one link per entry with its test id and destination', () => {
    const wrapper = mountSidebar()
    const links = wrapper.findAllComponents(RouterLinkStub)

    expect(links).toHaveLength(2)
    expect(links[0]?.props('to')).toBe('/integrations/payments')
    expect(wrapper.find('[data-testid="int-email"]').exists()).toBe(true)
  })

  it('draws each icon from its markup', () => {
    const wrapper = mountSidebar()

    expect(wrapper.find('[data-testid="int-payments"] svg rect').exists()).toBe(true)
    expect(wrapper.find('[data-testid="int-email"] svg circle').exists()).toBe(true)
  })

  it('marks only the active entry as the current page', () => {
    const wrapper = mountSidebar('email')

    expect(wrapper.get('[data-testid="int-email"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-testid="int-payments"]').attributes('aria-current')).toBeUndefined()
  })

  it('is drawn on the glass surface', () => {
    expect(panelClasses(mountSidebar())).toContain('glass')
  })

  it('is narrow until hovered or focused, then shows the captions', async () => {
    const wrapper = mountSidebar()

    expect(panelClasses(wrapper)).toContain('w-[56px]')

    await wrapper.trigger('mouseenter')
    expect(panelClasses(wrapper)).toContain('w-[230px]')

    await wrapper.trigger('mouseleave')
    expect(panelClasses(wrapper)).toContain('w-[56px]')
  })

  it('keeps only the rail width in the layout and opens over the content, on a denser surface, instead of widening its column', async () => {
    const wrapper = mountSidebar()

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['relative', 'shrink-0', 'w-[56px]']))
    expect(panelClasses(wrapper)).toEqual(expect.arrayContaining(['absolute', 'inset-y-0', 'left-0', 'z-20', 'glass']))

    await wrapper.trigger('mouseenter')

    expect(wrapper.classes()).toContain('w-[56px]')
    expect(wrapper.classes()).not.toContain('w-[230px]')
    expect(panelClasses(wrapper)).toEqual(expect.arrayContaining(['glass-strong', 'shadow-overlay']))
    expect(panelClasses(wrapper)).not.toContain('glass')
  })

  it('opens on the focus a ring is drawn for, but not on the focus a click or a tap gives an entry', async () => {
    const wrapper = mountSidebar()
    // Both are found before `matches` is stubbed: the stub would make every selector match the root element.
    const link = wrapper.get('[data-testid="int-payments"]')
    const panel = wrapper.get('[data-testid="int-panel"]')
    const matches = vi.spyOn(Element.prototype, 'matches')

    // A pointer press focuses the link without the focus ring.
    matches.mockImplementation(() => false)
    await link.trigger('focusin')
    expect(panel.classes()).toContain('w-[56px]')

    // The keyboard does draw it.
    matches.mockImplementation(() => true)
    await link.trigger('focusin')
    expect(panel.classes()).toContain('w-[230px]')
  })

  it('marks the active entry with the navigation pill tokens and idle entries with secondary text', () => {
    const wrapper = mountSidebar('email')

    expect(wrapper.get('[data-testid="int-email"]').classes()).toContain('bg-(--nav-active-bg)')
    expect(wrapper.get('[data-testid="int-payments"]').classes()).toContain('text-text-secondary')
  })

  it('folds to a rail of centred icons that keep the caption as tooltip and name, and scrolls on its own', () => {
    const wrapper = mountSidebar('email')
    const link = wrapper.get('[data-testid="int-email"]')

    expect(link.classes()).toEqual(expect.arrayContaining(['justify-center', 'px-0']))
    expect(link.attributes('title')).toBe('Email')
    expect(link.attributes('aria-label')).toBe('Email')
    expect(link.text()).toBe('')
    expect(panelClasses(wrapper)).toEqual(expect.arrayContaining(['overflow-y-auto', 'min-h-0']))
  })

  it('shows truncated captions once open, with the full title as tooltip', async () => {
    const wrapper = mount(UiSidebar, {
      props: { items: [{ ...items[0]!, title: 'Payment gateways' }], testid: 'int' },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    await wrapper.trigger('mouseenter')
    const link = wrapper.get('[data-testid="int-payments"]')

    expect(link.get('span').classes()).toContain('truncate')
    expect(link.text()).toBe('Payments')
    expect(link.attributes('title')).toBe('Payment gateways')
  })

  it('stays open while a field inside has focus even after the pointer leaves', async () => {
    const wrapper = mount(UiSidebar, {
      props: { items, testid: 'int' },
      slots: { header: '<input data-testid="head" />' },
      attachTo: document.body,
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    await wrapper.trigger('mouseenter')
    const field = wrapper.get('[data-testid="head"]')
    // A field that takes typing always shows the focus ring.
    const matches = vi.spyOn(Element.prototype, 'matches').mockImplementation(() => true)
    await field.trigger('focusin')
    matches.mockRestore()
    await wrapper.trigger('mouseleave')
    expect(panelClasses(wrapper)).toContain('w-[230px]')

    await field.trigger('focusout')
    expect(panelClasses(wrapper)).toContain('w-[56px]')

    wrapper.unmount()
  })

  it('tells its owner when it opens and folds', async () => {
    const wrapper = mountSidebar()

    await wrapper.trigger('mouseenter')
    await wrapper.trigger('mouseleave')

    expect(wrapper.emitted('update:expanded')).toEqual([[true], [false]])
  })

  it('draws a heading per group when open and a hairline in the rail, and the header slot only when open', async () => {
    const wrapper = mount(UiSidebar, {
      props: { items: [{ ...items[0]!, group: 'Money' }, { ...items[1]!, group: 'Money' }, { key: 'x', label: 'X', to: '/x', icon: '<rect/>', group: 'Other' }], testid: 'int' },
      slots: { header: '<input data-testid="head" />' },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    expect(wrapper.find('[data-testid="head"]').exists()).toBe(false)
    expect(wrapper.findAll('[aria-hidden="true"].border-t')).toHaveLength(2)

    await wrapper.trigger('mouseenter')

    expect(wrapper.find('[data-testid="head"]').exists()).toBe(true)
    expect(wrapper.findAll('p').map(heading => heading.text())).toEqual(['Money', 'Other'])
  })
})
