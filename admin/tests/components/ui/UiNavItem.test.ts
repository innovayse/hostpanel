/**
 * Unit tests for UiNavItem: the element it is (router link, anchor, button), the top-level and sub-menu looks, the
 * current page mark, the rail, the sub-menu chevron and the badge slot.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiNavItem from '@/components/ui/UiNavItem.vue'
import { Icon } from '@/lib/icons'
import { UiNavItemLevel } from '@/utils/ui/nav'

/**
 * Mounts an entry with the router link stubbed, so a `to` renders an anchor.
 *
 * @param props - Props of the entry (the label defaults to "Clients").
 * @param slots - Slots of the entry.
 * @returns The wrapper.
 */
const mountItem = (props: Record<string, unknown>, slots: Record<string, string> = {}) =>
  mount(UiNavItem, { props: { label: 'Clients', ...props }, slots, global: { stubs: { RouterLink: RouterLinkStub } } })

describe('UiNavItem', () => {
  it('draws a router link for a route, a plain anchor for an address and a button for neither', () => {
    expect(mountItem({ to: '/clients' }).findComponent(RouterLinkStub).props('to')).toBe('/clients')

    const anchor = mountItem({ href: 'https://docs.example.com', external: true })
    expect(anchor.element.tagName).toBe('A')
    expect(anchor.attributes('href')).toBe('https://docs.example.com')
    expect(anchor.attributes('target')).toBe('_blank')
    expect(anchor.attributes('rel')).toBe('noopener noreferrer')

    const button = mountItem({})
    expect(button.element.tagName).toBe('BUTTON')
    expect(button.attributes('type')).toBe('button')
  })

  it('opens an address in the same tab unless it is external', () => {
    const anchor = mountItem({ href: '/docs' })

    expect(anchor.attributes('target')).toBeUndefined()
    expect(anchor.attributes('rel')).toBeUndefined()
  })

  it('draws the icon and the label of a top-level entry at 42px and 14px', () => {
    const wrapper = mountItem({ href: '/clients', icon: Icon.Book })

    expect(wrapper.text()).toBe('Clients')
    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-[2.625rem]', 'text-sm', 'rounded-control', 'gap-3', 'px-3']))
  })

  it('marks the current page with the filled pill, the accent icon and aria-current, and an idle entry with neither', () => {
    const active = mountItem({ href: '/clients', icon: Icon.Book, active: true })
    const idle = mountItem({ href: '/clients', icon: Icon.Book })

    expect(active.classes()).toEqual(expect.arrayContaining(['bg-(--nav-active-bg)', 'text-text-primary']))
    expect(active.get('svg').classes()).toContain('text-text-accent')
    expect(active.attributes('aria-current')).toBe('page')
    expect(idle.classes()).toEqual(expect.arrayContaining(['border-transparent', 'text-text-secondary']))
    expect(idle.get('svg').classes()).toContain('text-text-muted')
    expect(idle.attributes('aria-current')).toBeUndefined()
  })

  it('never marks a button as the current page, even when it is the active section', () => {
    expect(mountItem({ active: true }).attributes('aria-current')).toBeUndefined()
  })

  it('folds to its icon alone in the rail, the label becoming the tooltip and the accessible name', () => {
    const wrapper = mountItem({ href: '/clients', icon: Icon.Book, rail: true })

    expect(wrapper.text()).toBe('')
    expect(wrapper.attributes('aria-label')).toBe('Clients')
    expect(wrapper.attributes('title')).toBe('Clients')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['justify-center', 'px-0']))
  })

  it('names itself by the hint when given one, and by nothing while its label is drawn', () => {
    expect(mountItem({ icon: Icon.Book, rail: true, hint: 'Clients, 2 waiting' }).attributes('aria-label')).toBe('Clients, 2 waiting')
    expect(mountItem({ icon: Icon.Book }).attributes('aria-label')).toBeUndefined()
  })

  it('reports whether its sub-menu is open and turns the chevron with it, with no chevron in the rail', () => {
    const closed = mountItem({ icon: Icon.Book, expanded: false })
    const open = mountItem({ icon: Icon.Book, expanded: true })

    expect(closed.attributes('aria-expanded')).toBe('false')
    expect(open.attributes('aria-expanded')).toBe('true')
    expect(closed.findAll('svg')).toHaveLength(2)
    expect(closed.findAll('svg')[1]?.classes()).not.toContain('rotate-90')
    expect(open.findAll('svg')[1]?.classes()).toContain('rotate-90')
    expect(mountItem({ icon: Icon.Book, expanded: true, rail: true }).findAll('svg')).toHaveLength(1)
    expect(mountItem({ icon: Icon.Book }).attributes('aria-expanded')).toBeUndefined()
  })

  it('draws a sub-menu link at 13px with its own piece of the guide, lit up to the active link', () => {
    const above = mountItem({ href: '/a', level: UiNavItemLevel.Sub, stop: 0, activeStop: 1 })
    const active = mountItem({ href: '/b', level: UiNavItemLevel.Sub, stop: 1, activeStop: 1, active: true })
    const below = mountItem({ href: '/c', level: UiNavItemLevel.Sub, stop: 2, activeStop: 1 })

    expect(above.classes()).toEqual(expect.arrayContaining(['nav-guide-row', 'min-h-8', 'text-[0.8125rem]', 'nav-guide-lit', '[--i:0]']))
    expect(above.classes()).not.toContain('nav-guide-active')
    expect(active.classes()).toEqual(expect.arrayContaining(['nav-guide-lit', 'nav-guide-active', '[--i:1]', 'bg-surface-hover', 'text-text-accent']))
    expect(below.classes()).not.toContain('nav-guide-lit')
    expect(below.classes()).toContain('text-text-muted')
  })

  it('keeps the label of a sub-menu link in the rail, which has no sub-menu to fold', () => {
    expect(mountItem({ href: '/a', level: UiNavItemLevel.Sub, rail: true }).text()).toBe('Clients')
  })

  it('draws the badge slot beside the label', () => {
    const wrapper = mountItem({ href: '/clients', icon: Icon.Book }, { badge: '<span data-testid="count">3</span>' })

    expect(wrapper.get('[data-testid="count"]').text()).toBe('3')
  })

  it('passes classes, data attributes and listeners to the element', async () => {
    const wrapper = mount(UiNavItem, { props: { label: 'Clients' }, attrs: { class: 'mt-2', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('mt-2')
    expect(wrapper.attributes('data-testid')).toBe('probe')
    await wrapper.trigger('click')
    expect(wrapper.emitted('click')).toHaveLength(1)
  })
})
