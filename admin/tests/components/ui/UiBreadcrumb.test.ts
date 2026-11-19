/**
 * Unit tests for UiBreadcrumb: linked parents, a current page that is not a link, the fold of a long trail into a
 * keyboard-reachable "..." menu, and the phone layout that keeps only the current page.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiBreadcrumb from '@/components/ui/UiBreadcrumb.vue'
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
})
