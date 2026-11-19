/**
 * Unit tests for UiLink: anchor for an address, router link for a route, safe external links.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiLink from '@/components/ui/UiLink.vue'
import { UiActionKind } from '@/utils/ui/actions'

describe('UiLink', () => {
  it('renders the native a with its slot', () => {
    const wrapper = mount(UiLink, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('A')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiLink, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('renders the address on a plain anchor', () => {
    const wrapper = mount(UiLink, { props: { href: '/docs' } })

    expect(wrapper.attributes('href')).toBe('/docs')
    expect(wrapper.attributes('target')).toBeUndefined()
  })

  it('opens an external address in a new tab without leaking the opener', () => {
    const wrapper = mount(UiLink, { props: { href: 'https://example.test', external: true } })

    expect(wrapper.attributes('target')).toBe('_blank')
    expect(wrapper.attributes('rel')).toBe('noopener noreferrer')
  })

  it('renders a router link for a route', () => {
    const wrapper = mount(UiLink, {
      props: { to: '/clients' },
      global: { stubs: { RouterLink: RouterLinkStub } },
    })

    expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/clients')
  })

  it('takes the accent look', () => {
    const wrapper = mount(UiLink, { props: { href: '/a', variant: 'link' } })

    expect(wrapper.classes()).toContain('text-text-accent')
  })

  it('sets the type size of a text link by size', () => {
    const wrapper = mount(UiLink, { props: { href: '/a', variant: 'link', size: 'sm' } })

    expect(wrapper.classes()).toContain('text-[0.8125rem]')
  })

  it('draws a button-like look as a pill when asked', () => {
    const pill = mount(UiLink, { props: { variant: 'ghost', size: 'sm', pill: true } })
    const plain = mount(UiLink, { props: { variant: 'ghost', size: 'sm' } })

    expect(pill.classes()).toContain('rounded-pill')
    expect(plain.classes()).not.toContain('rounded-pill')
  })

  it('draws the icon of its action kind before the label, on a route and on an address', () => {
    const route = mount(UiLink, { props: { to: '/x', action: UiActionKind.Add }, slots: { default: 'Add' }, global: { stubs: { RouterLink: RouterLinkStub } } })
    const anchor = mount(UiLink, { props: { href: '/x', action: UiActionKind.Add }, slots: { default: 'Add' } })

    expect(route.find('svg').exists()).toBe(true)
    expect(anchor.find('svg').exists()).toBe(true)
    expect(mount(UiLink, { props: { href: '/x' } }).find('svg').exists()).toBe(false)
  })
})
