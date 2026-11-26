/**
 * Unit tests for UiBreadcrumb: linked parents, a current page that is not a link, the fold of a long trail into a
 * keyboard-reachable "..." menu, the phone layout of the bar that keeps only the current page, and the page layout that keeps every step.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
import { UiBreadcrumbVariant } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Builds a trail of the given labels, every step but the last linked.
 *
 * @param labels - One label per step.
 * @returns The steps.
 */
const trail = (...labels: string[]): UiBreadcrumbItem[] =>
  labels.map((label, index) => (index < labels.length - 1 ? { label, to: `/${label}` } : { label }))

/**
 * Mounts the trail with the English catalogue and the router link stubbed.
 *
 * @param items - The trail.
 * @param props - Further props.
 * @returns The wrapper.
 */
const mountTrail = (items: UiBreadcrumbItem[], props: Record<string, unknown> = {}) =>
  mount(UiBreadcrumb, {
    props: { items, ...props },
    global: { plugins: [createTestI18n()], stubs: { RouterLink: RouterLinkStub, teleport: true } },
  })

describe('UiBreadcrumb', () => {
  it('is a labelled navigation landmark', () => {
    const wrapper = mountTrail(trail('Settings', 'General'))

    expect(wrapper.get('nav').attributes('aria-label')).toBe('Breadcrumb')
    expect(wrapper.find('ol').exists()).toBe(true)
  })

  it('draws a step with an onSelect as a button that runs it, for a trail kept in the screen state', async () => {
    let chosen = 0
    const wrapper = mountTrail([
      { label: 'Home', onSelect: () => { chosen += 1 }, testid: 'trail-home' },
      { label: 'Drivers' },
    ])

    await wrapper.get('[data-testid="trail-home"]').trigger('click')

    expect(chosen).toBe(1)
    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0)
    expect(wrapper.get('[data-testid="ui-breadcrumb-current"]').text()).toBe('Drivers')
  })

  it('links every step but the last, which is the current page and no link', () => {
    const wrapper = mountTrail(trail('Settings', 'Slides', 'Edit'))
    const links = wrapper.findAllComponents(RouterLinkStub)
    const current = wrapper.get('[data-testid="ui-breadcrumb-current"]')

    expect(links.map((link) => link.text())).toEqual(['Settings', 'Slides'])
    expect(links.map((link) => link.props('to'))).toEqual(['/Settings', '/Slides'])
    expect(current.text()).toBe('Edit')
    expect(current.attributes('aria-current')).toBe('page')
    expect(current.element.tagName).not.toBe('A')
  })

  it('names the current page with the test id the screen gives', () => {
    const wrapper = mountTrail(trail('Servers'), { currentTestid: 'topbar-title' })

    expect(wrapper.get('[data-testid="topbar-title"]').text()).toBe('Servers')
  })

  it('draws no fold for a trail that fits', () => {
    expect(mountTrail(trail('a', 'b', 'c', 'd')).find('[data-testid="ui-breadcrumb-more"]').exists()).toBe(false)
  })

  it('folds the middle of a long trail into a button that names what it holds', () => {
    const wrapper = mountTrail(trail('a', 'b', 'c', 'd', 'e'))
    const more = wrapper.get('[data-testid="ui-breadcrumb-more"]')

    expect(more.attributes('aria-label')).toBe('Show hidden pages')
    expect(more.attributes('aria-haspopup')).toBe('menu')
    expect(wrapper.findAllComponents(RouterLinkStub).map((link) => link.text())).toEqual(['a', 'd'])
    expect(wrapper.get('[data-testid="ui-breadcrumb-current"]').text()).toBe('e')
  })

  it('opens the hidden steps as links in a menu, and closes it on Escape', async () => {
    const wrapper = mountTrail(trail('a', 'b', 'c', 'd', 'e'))

    await wrapper.get('[data-testid="ui-breadcrumb-more"]').trigger('click')
    const panel = wrapper.get('[data-testid="ui-menu-panel"]')

    expect(panel.attributes('role')).toBe('menu')
    expect(panel.findAll('[role="menuitem"]').map((item) => item.text())).toEqual(['b', 'c'])
    await wrapper.get('[data-testid="ui-breadcrumb-more"]').trigger('keydown', { key: 'Escape' })
    expect(wrapper.find('[data-testid="ui-menu-panel"]').exists()).toBe(false)
  })

  it('shows only the current page below md, and the parents from md up', () => {
    const wrapper = mountTrail(trail('Settings', 'Slides', 'Edit'))
    const items = wrapper.findAll('li')

    expect(items.map((item) => item.classes().includes('max-md:hidden'))).toEqual([true, true, false])
  })

  it('draws nothing for an empty trail', () => {
    expect(mountTrail([]).find('nav').exists()).toBe(false)
  })

  describe('page variant', () => {
    it('keeps every step at every width, where the bar variant hides the parents below md', () => {
      const wrapper = mountTrail(trail('Clients', 'Ada', 'Domains', 'example.com'), { variant: UiBreadcrumbVariant.Page })

      expect(wrapper.findAll('li').flatMap((item) => item.classes())).not.toContain('max-md:hidden')
      expect(wrapper.findAllComponents(RouterLinkStub).map((link) => link.text())).toEqual(['Clients', 'Ada', 'Domains'])
    })

    it('separates the steps with the chevron of the icon library and lets a long trail wrap', () => {
      const wrapper = mountTrail(trail('Clients', 'Ada', 'Domains', 'example.com'), { variant: UiBreadcrumbVariant.Page })

      expect(wrapper.findAll('li svg')).toHaveLength(3)
      expect(wrapper.get('ol').classes()).toEqual(expect.arrayContaining(['flex-wrap', 'text-sm']))
    })

    it('cuts a long name with an ellipsis and keeps its full text in the title', () => {
      const name = 'A very long client name that cannot fit the line'
      const wrapper = mountTrail(trail('Clients', name, 'Domains', 'example.com'), { variant: UiBreadcrumbVariant.Page })
      const link = wrapper.findAllComponents(RouterLinkStub)[1]!

      expect(link.classes()).toContain('truncate')
      expect(link.attributes('title')).toBe(name)
      expect(link.element.parentElement?.classList.contains('min-w-0')).toBe(true)
      expect(wrapper.get('[data-testid="ui-breadcrumb-current"]').classes()).toContain('truncate')
    })

    it('mutes the steps: parents in the muted tone, the page itself in the secondary one, and no link on the page itself', () => {
      const wrapper = mountTrail(trail('Clients', 'Ada'), { variant: UiBreadcrumbVariant.Page })
      const current = wrapper.get('[data-testid="ui-breadcrumb-current"]')

      expect(wrapper.findComponent(RouterLinkStub).classes()).toContain('text-text-muted')
      expect(current.classes()).toContain('text-text-secondary')
      expect(current.element.tagName).not.toBe('A')
      expect(current.attributes('aria-current')).toBe('page')
    })

    it('draws a bar in place of the page whose name is still being read', () => {
      const wrapper = mountTrail([{ label: 'Clients', to: '/clients' }, { label: '', pending: true }], { variant: UiBreadcrumbVariant.Page })

      expect(wrapper.find('[data-testid="ui-breadcrumb-current-pending"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="ui-breadcrumb-current"]').exists()).toBe(false)
    })
  })
})
