/**
 * Unit tests for UiBackLink: a ghost pill link with the back arrow and the short name of its destination.
 */
import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import UiBackLink from '@/components/ui/UiBackLink.vue'

/**
 * Mounts the link with the router link stubbed.
 *
 * @param attrs - Extra attributes for the link.
 * @returns The wrapper.
 */
const mountLink = (attrs: Record<string, string> = {}) =>
  mount(UiBackLink, {
    props: { to: '/plugins', label: 'Plugins' },
    attrs,
    global: { stubs: { RouterLink: RouterLinkStub } },
  })

describe('UiBackLink', () => {
  it('links to the route with the arrow and the short label', () => {
    const wrapper = mountLink()

    expect(wrapper.findComponent(RouterLinkStub).props('to')).toBe('/plugins')
    expect(wrapper.text()).toBe('Plugins')
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('is a ghost pill from the theme tokens, with no underline', () => {
    const classes = mountLink().classes().join(' ')

    expect(classes).toContain('rounded-pill')
    expect(classes).toContain('text-text-secondary')
    expect(classes).toContain('hover:bg-surface-hover')
    expect(classes).toContain('whitespace-nowrap')
    expect(classes).not.toContain('underline')
  })

  it('keeps the test id and classes of the screen', () => {
    const wrapper = mountLink({ 'data-testid': 'probe', class: 'self-start' })

    expect(wrapper.attributes('data-testid')).toBe('probe')
    expect(wrapper.classes()).toContain('self-start')
  })

  it('shows on every screen size by default, and only below md when the breadcrumb covers its target', () => {
    const wrapper = mount(UiBackLink, { props: { to: '/plugins', label: 'Plugins', mobileOnly: true }, global: { stubs: { RouterLink: RouterLinkStub } } })

    expect(mountLink().classes()).not.toContain('md:hidden')
    expect(wrapper.classes()).toContain('md:hidden')
  })

  it('draws the pill with its label and no link when it has no target', () => {
    const wrapper = mount(UiBackLink, { props: { label: 'Plugins' } })

    expect(wrapper.text()).toBe('Plugins')
    expect(wrapper.find('a').exists()).toBe(false)
  })
})
