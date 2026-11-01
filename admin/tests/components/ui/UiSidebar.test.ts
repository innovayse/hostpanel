/**
 * Unit tests for UiSidebar: entries as links with icons drawn without v-html, active marker and
 * hover expansion.
 */
import { describe, expect, it, vi } from 'vitest'
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

  it('keeps its narrow slot and floats the expanded panel over the content', async () => {
    vi.useFakeTimers()
    const wrapper = mountSidebar()
    const panel = wrapper.get('[data-testid="ui-sidebar-panel"]')

    expect(wrapper.classes()).toContain('w-[48px]')
    expect(panel.classes()).toContain('absolute')
    expect(panel.classes()).toContain('w-[48px]')

    await panel.trigger('mouseenter')
    await vi.advanceTimersByTimeAsync(130)
    expect(panel.classes()).toContain('w-[260px]')
    expect(wrapper.classes()).toContain('w-[48px]')

    await panel.trigger('mouseleave')
    await vi.advanceTimersByTimeAsync(260)
    expect(panel.classes()).toContain('w-[48px]')

    await panel.trigger('focusin')
    expect(panel.classes()).toContain('w-[260px]')
    vi.useRealTimers()
  })

  it('does not restart the animation when the pointer leaves and returns quickly', async () => {
    vi.useFakeTimers()
    const panel = mountSidebar().get('[data-testid="ui-sidebar-panel"]')

    await panel.trigger('mouseenter')
    await vi.advanceTimersByTimeAsync(130)
    await panel.trigger('mouseleave')
    await vi.advanceTimersByTimeAsync(100)
    await panel.trigger('mouseenter')
    await vi.advanceTimersByTimeAsync(400)

    expect(panel.classes()).toContain('w-[260px]')
    vi.useRealTimers()
  })

  it('colours the active entry with the primary accent and idle entries with secondary text', () => {
    const wrapper = mountSidebar('email')

    expect(wrapper.get('[data-testid="int-email"]').classes()).toContain('text-primary-500')
    expect(wrapper.get('[data-testid="int-payments"]').classes()).toContain('text-text-secondary')
  })

  it('gives captions a fixed width so they never re-wrap while the panel grows', () => {
    const label = mountSidebar().get('a span:not(.absolute)')

    expect(label.classes()).toContain('w-[204px]')
    expect(label.classes()).toContain('shrink-0')
  })
})
