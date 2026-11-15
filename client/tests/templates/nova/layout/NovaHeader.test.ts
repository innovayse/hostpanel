/**
 * Unit tests for NovaHeader: navigation from content, the cart count, the sign-in target and the
 * mobile disclosure (open, close on Escape, focus return, Tab wrap).
 */
import { describe, expect, it } from 'vitest'
import { useCartStore } from '~/stores/cart'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import NovaHeader from '~/templates/nova/layout/NovaHeader.vue'
import { NAV_ITEMS } from '~/templates/nova/content'
import { stubTemplateGlobals } from '~/tests/helpers/templateGlobals'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'

/**
 * Mounts the header in the document so focus and document listeners work.
 *
 * @param props Props for the header.
 * @returns The wrapper.
 */
const mountHeader = (props: Record<string, unknown> = {}) =>
  mountWithKit(NovaHeader, { props, attachToBody: true })

describe('NovaHeader', () => {
  it('links every navigation entry and marks the current one', () => {
    stubTemplateGlobals()
    const wrapper = mountHeader({ active: NAV_ITEMS[1]!.key })

    for (const item of NAV_ITEMS) {
      expect(wrapper.find(`[data-testid="nova-nav-${item.key}"]`).attributes('href')).toBe(item.to)
    }
    expect(wrapper.find(`[data-testid="nova-nav-${NAV_ITEMS[1]!.key}"]`).attributes('aria-current')).toBe('page')
    expect(wrapper.find(`[data-testid="nova-nav-${NAV_ITEMS[0]!.key}"]`).attributes('aria-current')).toBeUndefined()
  })

  it('points the skip link at the page main region', () => {
    stubTemplateGlobals()

    expect(mountHeader().find('[data-testid="nova-skip-link"]').attributes('href')).toBe('#nova-main')
  })

  it('draws the bundled cube mark and the site name, never a letter tile, when no logo is uploaded', () => {
    stubTemplateGlobals({ siteName: 'Acme Host' })
    const logo = mountHeader().find('[data-testid="nova-nav-logo"]')

    expect(logo.text()).toBe('Acme Host')
    expect(logo.find('[data-testid="app-logo-mark"]').attributes('src')).toContain('/favicon.svg')
  })

  it('renders the uploaded logo instead of the mark', () => {
    stubTemplateGlobals({ logo: '/api/uploads/logo.png' })
    const logo = mountHeader().find('[data-testid="nova-nav-logo"]')

    expect(logo.find('[data-testid="app-logo-light"]').attributes('src')).toBe('/api/uploads/logo.png')
    expect(logo.find('[data-testid="app-logo-mark"]').exists()).toBe(false)
  })

  it('shows the cart count only when the cart has lines', async () => {
    stubTemplateGlobals()
    const wrapper = mountHeader()

    expect(wrapper.find('[data-testid="nova-nav-cart-count"]').exists()).toBe(false)

    // Two placeholder lines: the badge reads the length, not the line contents.
    useCartStore().items.push({} as never, {} as never)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="nova-nav-cart-count"]').text()).toBe('2')
  })

  it('sends the sign-in action to the deployment-chosen target', () => {
    stubTemplateGlobals()

    expect(mountHeader().find('[data-testid="nova-nav-login"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
  })

  it('opens the mobile panel with aria-expanded and closes it on Escape, returning focus', async () => {
    stubTemplateGlobals()
    const wrapper = mountHeader()
    const toggle = wrapper.find('[data-testid="nova-nav-menu-toggle"]')

    expect(wrapper.find('#nova-mobile-menu').exists()).toBe(false)
    expect(toggle.attributes('aria-expanded')).toBe('false')

    await toggle.trigger('click')

    expect(wrapper.find('#nova-mobile-menu').exists()).toBe(true)
    expect(toggle.attributes('aria-expanded')).toBe('true')

    await wrapper.find('#nova-mobile-menu').trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('#nova-mobile-menu').exists()).toBe(false)
    expect(document.activeElement).toBe(toggle.element)
  })

  it('closes the panel when a mobile link is followed', async () => {
    stubTemplateGlobals()
    const wrapper = mountHeader()

    await wrapper.find('[data-testid="nova-nav-menu-toggle"]').trigger('click')
    await wrapper.find(`[data-testid="nova-mobile-${NAV_ITEMS[0]!.key}"]`).trigger('click')

    expect(wrapper.find('#nova-mobile-menu').exists()).toBe(false)
  })

  it('wraps Tab from the last panel entry back to the first', async () => {
    stubTemplateGlobals()
    const wrapper = mountHeader()

    await wrapper.find('[data-testid="nova-nav-menu-toggle"]').trigger('click')
    const entries = wrapper.findAll('#nova-mobile-menu a[href]')
    const last = entries[entries.length - 1]!.element as HTMLElement
    last.focus()

    await wrapper.find('#nova-mobile-menu').trigger('keydown', { key: 'Tab' })

    expect(document.activeElement).toBe(entries[0]!.element)
  })
})
