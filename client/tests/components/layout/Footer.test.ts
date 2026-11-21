/**
 * Unit tests for LayoutFooter: the call-to-action band, the five link columns (hosting groups
 * from the catalogue), the brand block with the operator's contact details and trust marks, and
 * the bottom row with the pickers and theme toggle.
 *
 * The real catalogue store, API composable, transport, logo lockup and the operator's settings composables run
 * against a stub server playing the BFF; the pickers, the theme toggle and the domain search run for real too.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { TEST_LOCALE_PREFIX, localized, stubLocalePath, stubSwitchLocalePath } from '~/tests/helpers/runtimeStubs'
import { AMD_CURRENCY, USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import LayoutFooter from '~/components/layout/Footer.vue'
import SectionsDomainSearch from '~/components/sections/SectionsDomainSearch.vue'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useContactDetails } from '~/composables/useContactDetails'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { waitUntil } from '~/tests/helpers/waitUntil'
import { SITE_ABOUT_PATH, SITE_ACCEPTABLE_USE_PATH, SITE_COOKIE_POLICY_PATH, SITE_DOMAINS_PATH, SITE_DOMAINS_TRANSFER_PATH, SITE_FAQ_PATH, SITE_KNOWLEDGEBASE_PATH, SITE_PRIVACY_PATH, SITE_PRODUCTS_PATH, SITE_REFUND_POLICY_PATH, SITE_TERMS_PATH } from '~/constants/siteRoutes'

/** What the stub answers, by route, and the accessor of the stub the hooks start and close around each test. */
const { replies, stub } = usePortalSettingsServer({ useAppColorMode, useBrandLogo, useContactDetails, useLocalizedSetting, useSiteIdentity })

/** The plan groups the stub answers the catalogue read with; a test sets them before it mounts. */
let groupsOnOffer: PortalProductGroup[] = []

/** The contact settings the operator has entered, by their dotted key; a test replaces them before it mounts. */
let contact: Record<string, string> = {}

/** An operator who has entered an e-mail address and a phone number, and no address. */
const CONTACT_SET: Record<string, string> = {
  [PortalSettingKey.ContactEmail]: 'hello@portal.test',
  [PortalSettingKey.ContactPhone]: '+1 555 010 200',
}

/**
 * Mounts the footer and waits until the catalogue groups it loads on mount are in the store.
 *
 * @param groups The plan groups the catalogue holds.
 * @returns The wrapper.
 */
const mountFooter = async (groups: PortalProductGroup[] = []) => {
  groupsOnOffer = groups
  replies[readOf(CatalogPath.ProductGroups)] = () => ({ body: groupsOnOffer })
  replies[readOf(CatalogPath.BillingCurrencies)] = { body: [USD_CURRENCY, AMD_CURRENCY] }
  await loadPortalSettings(replies, contact)
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mountWithKit(LayoutFooter, {
    pinia,
    components: { SectionsDomainSearch },
  })
  await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups)).toHaveLength(1))
  await awaitCatalogue()
  return wrapper
}

describe('LayoutFooter', () => {
  beforeEach(() => {
    stubLocalePath(TEST_LOCALE_PREFIX)
    stubSwitchLocalePath()
    contact = CONTACT_SET
  })

  it('sits on the classic footer surface with a visible top border', async () => {
    const classes = (await mountFooter()).find('footer').classes()

    expect(classes).toEqual(expect.arrayContaining(['bg-classic-footer', 'border-t', 'border-classic-border']))
  })

  it('drops the old decor: no dot headings, blobs, pill bar or back-to-top button', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('[data-testid="footer-back-to-top"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('blur-[120px]')
    expect(wrapper.html()).not.toContain('rounded-[2rem]')
    expect(wrapper.text()).not.toContain('footer.bottom.secure')
  })

  it('puts the shared domain search beside a get started link in the call to action', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('[data-testid="footer-cta"] [data-testid="sections-domain-search"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="footer-cta-plans"]').attributes('href')).toBe(localized(SITE_PRODUCTS_PATH))
  })

  it('renders the five link columns in order', async () => {
    const wrapper = await mountFooter()

    const titles = wrapper.findAll('h3').map((title) => title.text())
    expect(titles).toEqual([
      'footer.columns.hosting',
      'footer.columns.domains',
      'footer.columns.company',
      'footer.columns.support',
      'footer.columns.legal',
    ])
  })

  it('lists every catalogue plan group in the hosting column, skipping empty ones', async () => {
    const wrapper = await mountFooter([
      { id: 1, name: 'Shared hosting', productCount: 3 },
      { id: 7, name: 'Cloud servers', productCount: 2 },
      { id: 10, name: 'Empty', productCount: 0 },
    ])

    const column = wrapper.find('[data-testid="footer-column-hosting"]')
    const labels = column.findAll('a').map((link) => link.text())
    expect(labels).toContain('Shared hosting')
    expect(labels).toContain('Cloud servers')
    expect(labels).not.toContain('Empty')
    expect(column.find('[data-testid="footer-hosting-all"]').attributes('href')).toBe(localized(SITE_PRODUCTS_PATH))
  })

  it('links the domain, company, support and legal pages', async () => {
    const wrapper = await mountFooter()

    /**
     * Reads the destination of the footer link with the given test id.
     *
     * @param testid Test id of the link.
     * @returns Its `href`, or undefined when it has none.
     */
    const href = (testid: string): string | undefined => wrapper.find(`[data-testid="${testid}"]`).attributes('href')
    expect(href('footer-domains-register')).toBe(localized(SITE_DOMAINS_PATH))
    expect(href('footer-domains-transfer')).toBe(localized(SITE_DOMAINS_TRANSFER_PATH))
    expect(href('footer-company-about')).toBe(localized(SITE_ABOUT_PATH))
    expect(href('footer-support-help')).toBe(localized(SITE_KNOWLEDGEBASE_PATH))
    expect(href('footer-support-faq')).toBe(localized(SITE_FAQ_PATH))
    expect(href('footer-legal-privacy')).toBe(localized(SITE_PRIVACY_PATH))
    expect(href('footer-legal-cookies')).toBe(localized(SITE_COOKIE_POLICY_PATH))
    expect(href('footer-legal-terms')).toBe(localized(SITE_TERMS_PATH))
    expect(href('footer-legal-refund')).toBe(localized(SITE_REFUND_POLICY_PATH))
    expect(href('footer-legal-acceptableUse')).toBe(localized(SITE_ACCEPTABLE_USE_PATH))
  })

  it('shows the logo lockup and the tagline in the brand block', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('[data-testid="app-logo-mark"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="footer-brand"]').text()).toContain('footer.tagline')
  })

  it('offers the contact email and phone as mailto and tel text links', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('[data-testid="footer-email"]').attributes('href')).toBe('mailto:hello@portal.test')
    expect(wrapper.find('[data-testid="footer-phone"]').attributes('href')).toBe('tel:+1555010200')
  })

  it('hides the contact rows that are not configured', async () => {
    contact = {}
    const wrapper = await mountFooter()

    expect(wrapper.find('[data-testid="footer-email"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="footer-phone"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="footer-address"]').exists()).toBe(false)
  })

  it('shows the address when one is configured', async () => {
    contact = { ...CONTACT_SET, [PortalSettingKey.ContactAddress]: '1 Example Street' }

    expect((await mountFooter()).find('[data-testid="footer-address"]').text()).toBe('1 Example Street')
  })

  it('draws generic trust marks as icons with plain labels and no images', async () => {
    const trust = (await mountFooter()).find('[data-testid="footer-trust"]')

    expect(trust.findAll('li')).toHaveLength(3)
    expect(trust.findAll('svg')).toHaveLength(3)
    expect(trust.find('img').exists()).toBe(false)
  })

  it('puts the copyright, both pickers and the theme toggle in the bottom row', async () => {
    const bottom = (await mountFooter()).find('[data-testid="footer-bottom"]')

    expect(bottom.text()).toContain(String(new Date().getFullYear()))
    expect(bottom.text()).toContain('footer.bottom.rights')
    expect(bottom.find('[data-testid="language-switcher"]').exists()).toBe(true)
    expect(bottom.find('[data-testid="currency-switcher"]').exists()).toBe(true)
    expect(bottom.find('[data-testid="theme-toggle"]').exists()).toBe(true)
  })

  it('stacks the columns on a phone and spreads them from the small breakpoint', async () => {
    const grid = (await mountFooter()).find('[data-testid="footer-brand"]').element.parentElement!

    expect(grid.className).toContain('grid-cols-1')
    expect(grid.className).toContain('sm:grid-cols-3')
  })

  it('adds no top margin, so no gap is stacked above the footer', async () => {
    const wrapper = await mountFooter()

    expect(wrapper.find('footer').classes().filter((name) => /^-?m[ty]?-/.test(name))).toEqual([])
  })
})
