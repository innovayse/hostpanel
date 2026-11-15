/**
 * Unit tests for AuroraHeader: navigation, the brand mark or the uploaded logo pair, the cart
 * button, signed-in versus signed-out controls, and the narrow-screen menu.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { reactive } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraHeader from '~/templates/aurora/layout/AuroraHeader.vue'
import { HomeSectionId } from '~/types/homesectionid'
import { stubAuroraGlobals } from '../auroraGlobals'
import { CLIENT_DASHBOARD_PATH, CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'

/**
 * Stands in for the auto-imported `useRoute`.
 *
 * @param hash - The route hash to report.
 */
const stubRoute = (hash = ''): void => {
  vi.stubGlobal('useRoute', () => reactive({ path: '/', hash }))
}

/** The language switcher and theme toggle have their own tests; here they only need to exist. */
const STUBS = { UiLanguageSwitcher: true, UiThemeToggle: true }

describe('AuroraHeader', () => {
  beforeEach(() => {
    stubRoute()
  })

  it('links the five navigation entries through the ids the e2e specs use', () => {
    stubAuroraGlobals()
    stubRoute()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    expect(wrapper.find('[data-testid="nav-hosting"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="nav-domains"]').attributes('href')).toBe('/domains')
    expect(wrapper.find('[data-testid="nav-services"]').attributes('href')).toBe('/#services')
    expect(wrapper.find('[data-testid="nav-process"]').attributes('href')).toBe('/#process')
    expect(wrapper.find('[data-testid="nav-faq"]').attributes('href')).toBe('/faq')
    expect(wrapper.find('[data-testid="nav-cart"]').attributes('href')).toBe('/cart')
  })

  it('emphasises the current entry only', () => {
    stubAuroraGlobals()
    stubRoute()
    const wrapper = mountWithKit(AuroraHeader, { props: { active: 'domains' }, stubs: STUBS })

    expect(wrapper.find('[data-testid="nav-domains"]').classes()).toContain('font-semibold')
    expect(wrapper.find('[data-testid="nav-hosting"]').classes()).not.toContain('font-semibold')
    expect(wrapper.find('[data-testid="nav-domains"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-testid="nav-hosting"]').attributes('aria-current')).toBeUndefined()
  })

  it('marks the home-page section named by the route hash as current', () => {
    stubAuroraGlobals()
    stubRoute(`#${HomeSectionId.Process}`)
    const wrapper = mountWithKit(AuroraHeader, { props: { active: 'hosting' }, stubs: STUBS })

    expect(wrapper.find('[data-testid="nav-process"]').classes()).toEqual(expect.arrayContaining(['font-semibold', 'text-ac1']))
    expect(wrapper.find('[data-testid="nav-hosting"]').classes()).not.toContain('font-semibold')
  })

  it('gives the entries and Sign in the primary text colour and a brand hover', () => {
    stubAuroraGlobals()
    stubRoute()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    for (const id of ['nav-hosting', 'nav-sign-in']) {
      expect(wrapper.find(`[data-testid="${id}"]`).classes()).toEqual(expect.arrayContaining(['text-tx', 'hover:text-ac1', 'hover:after:scale-x-100']))
    }
  })

  it('draws the cart and the menu toggle in the same frame, with the count badge on the cart', () => {
    stubAuroraGlobals()
    stubRoute()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    for (const id of ['nav-cart', 'nav-menu-toggle']) {
      expect(wrapper.find(`[data-testid="${id}"]`).classes()).toEqual(expect.arrayContaining(['h-10', 'w-10', 'rounded-xl', 'border-line2']))
    }
    expect(wrapper.find('[data-testid="nav-cart"]').classes()).toContain('relative')
  })

  it('hands the pickers and the toggle the shared control frame', () => {
    stubAuroraGlobals()
    stubRoute()
    const wrapper = mountWithKit(AuroraHeader, { stubs: { UiLanguageSwitcher: true, UiCurrencySwitcher: true } })

    for (const name of ['UiLanguageSwitcher', 'UiCurrencySwitcher']) {
      expect(wrapper.findComponent({ name }).props('triggerClass')).toEqual(expect.stringContaining('rounded-xl border border-line2'))
    }
    expect(wrapper.find('[data-testid="theme-toggle"]').classes()).toEqual(expect.arrayContaining(['h-10', 'w-10', 'rounded-xl', 'border-line2']))
  })

  it('draws the bundled cube mark and the site name, never a letter tile, when no logo is uploaded', () => {
    stubAuroraGlobals({ siteName: 'Acme Host' })
    const home = mountWithKit(AuroraHeader, { stubs: STUBS }).find('[data-testid="site-home"]')

    expect(home.text()).toBe('Acme Host')
    expect(home.find('[data-testid="app-logo-mark"]').attributes('src')).toContain('/favicon.svg')
    expect(home.find('[data-testid="app-logo-light"]').exists()).toBe(false)
  })

  it('renders only the light logo, as a plain image, when there is no dark variant', () => {
    // The real brand pair falls back each side to the other, so one upload is both URLs.
    stubAuroraGlobals({ logo: '/api/uploads/logo.png', logoDark: '/api/uploads/logo.png' })
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })
    const light = wrapper.find('[data-testid="app-logo-light"]')

    expect(light.attributes('src')).toBe('/api/uploads/logo.png')
    expect(light.classes()).not.toContain('dark:hidden')
    expect(wrapper.find('[data-testid="app-logo-dark"]').exists()).toBe(false)
  })

  it('renders both logos and swaps them with the dark variant, never with script', () => {
    stubAuroraGlobals({ logo: '/light.png', logoDark: '/dark.png' })
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    expect(wrapper.find('[data-testid="app-logo-light"]').classes()).toContain('dark:hidden')
    expect(wrapper.find('[data-testid="app-logo-dark"]').classes()).toEqual(expect.arrayContaining(['hidden', 'dark:block']))
  })

  it('offers sign in and get started to a visitor, and no account menu', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    expect(wrapper.find('[data-testid="nav-sign-in"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
    expect(wrapper.find('[data-testid="nav-start"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="account-menu-trigger"]').exists()).toBe(false)
  })

  it('swaps the sign-in pair for the account menu once there is a session', () => {
    stubAuroraGlobals({ user: { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' } })
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    expect(wrapper.find('[data-testid="account-menu-trigger"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nav-sign-in"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-start"]').exists()).toBe(false)
  })

  it('shows no count badge on an empty cart', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })

    expect(wrapper.find('[data-testid="nav-cart-count"]').exists()).toBe(false)
  })

  it('opens the narrow-screen menu and closes it when a link is chosen', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })
    expect(wrapper.find('[data-testid="nav-mobile-menu"]').exists()).toBe(false)

    await wrapper.find('[data-testid="nav-menu-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="nav-mobile-menu"]').exists()).toBe(true)

    await wrapper.find('[data-testid="nav-mobile-faq"]').trigger('click')
    expect(wrapper.find('[data-testid="nav-mobile-menu"]').exists()).toBe(false)
  })

  it('carries sign in and get started in the narrow-screen menu for a visitor', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })
    await wrapper.find('[data-testid="nav-menu-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="nav-mobile-sign-in"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
    expect(wrapper.find('[data-testid="nav-mobile-start"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="nav-mobile-account"]').exists()).toBe(false)
  })

  it('carries the client-area link instead of the pair once signed in', async () => {
    stubAuroraGlobals({ user: { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' } })
    const wrapper = mountWithKit(AuroraHeader, { stubs: STUBS })
    await wrapper.find('[data-testid="nav-menu-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="nav-mobile-account"]').attributes('href')).toBe(CLIENT_DASHBOARD_PATH)
    expect(wrapper.find('[data-testid="nav-mobile-sign-in"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nav-mobile-start"]').exists()).toBe(false)
  })
})
