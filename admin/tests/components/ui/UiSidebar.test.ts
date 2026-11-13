/**
 * Unit tests for UiSidebar: entries as links with icons drawn without v-html, active marker and
 * hover expansion.
 */
import { describe, expect, it } from 'vitest'
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

describe('UiSidebar', () => {
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
    expect(mountSidebar().classes()).toContain('glass')
  })

  it('is narrow until hovered or focused, then shows the captions', async () => {
    const wrapper = mountSidebar()

    expect(wrapper.classes()).toContain('w-[56px]')

    await wrapper.trigger('mouseenter')
    expect(wrapper.classes()).toContain('w-[230px]')

    await wrapper.trigger('mouseleave')
    expect(wrapper.classes()).toContain('w-[56px]')

    await wrapper.trigger('focusin')
    expect(wrapper.classes()).toContain('w-[230px]')
  })

  it('marks the active entry with the navigation pill tokens and idle entries with secondary text', () => {
    const wrapper = mountSidebar('email')

    expect(wrapper.get('[data-testid="int-email"]').classes()).toContain('bg-(--nav-active-bg)')
    expect(wrapper.get('[data-testid="int-payments"]').classes()).toContain('text-text-secondary')
  })
})
