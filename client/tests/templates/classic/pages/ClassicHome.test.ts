/**
 * Unit tests for ClassicHome: the hidden H1, the marketing sections in hosting-site order, and
 * the per-section visibility toggles.
 *
 * Every section runs for real, with the real catalogue, currency and storefront stores, the real composables and the
 * real transport against a stub server that answers each public read; only the carousel library is stood in for.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { VueWrapper } from '@vue/test-utils'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { mountAsyncPage } from '~/tests/helpers/mountWithKit'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { homeSectionSettings, loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { until, untilStable } from '~/tests/helpers/settle'
import { faqFixture, locationFixture, partnerFixture, testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import ProductFamilyCard from '~/components/catalog/ProductFamilyCard.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import SectionsApps from '~/components/sections/SectionsApps.vue'
import SectionsCTA from '~/components/sections/SectionsCTA.vue'
import SectionsDomainSearch from '~/components/sections/SectionsDomainSearch.vue'
import SectionsFAQ from '~/components/sections/SectionsFAQ.vue'
import SectionsGuarantees from '~/components/sections/SectionsGuarantees.vue'
import SectionsHero from '~/components/sections/SectionsHero.vue'
import SectionsLocations from '~/components/sections/SectionsLocations.vue'
import SectionsPartners from '~/components/sections/SectionsPartners.vue'
import SectionsPerformance from '~/components/sections/SectionsPerformance.vue'
import SectionsProcess from '~/components/sections/SectionsProcess.vue'
import SectionsProducts from '~/components/sections/SectionsProducts.vue'
import SectionsProductsFullSlider from '~/components/sections/SectionsProductsFullSlider.vue'
import SectionsProductsSlide from '~/components/sections/SectionsProductsSlide.vue'
import SectionsSecurity from '~/components/sections/SectionsSecurity.vue'
import SectionsSupport from '~/components/sections/SectionsSupport.vue'
import SectionsTestimonials from '~/components/sections/SectionsTestimonials.vue'
import SectionsWhyChooseUs from '~/components/sections/SectionsWhyChooseUs.vue'
import { useGuarantees } from '~/composables/useGuarantees'
import { useHomeSections } from '~/composables/useHomeSections'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { useTemplate } from '~/composables/useTemplate'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { HOME_SECTION_DEFAULT, HomeSection } from '~/constants/homeSection'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'
import ClassicHome from '~/templates/classic/pages/ClassicHome.vue'

vi.mock('swiper/vue', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    /** Stand-in carousel: renders its slides in a plain element. */
    Swiper: defineComponent({ name: 'Swiper', setup: (_props, { slots }) => () => h('div', slots.default?.()) }),
    /** Stand-in slide. */
    SwiperSlide: defineComponent({ name: 'SwiperSlide', setup: (_props, { slots }) => () => h('div', slots.default?.()) }),
  }
})
vi.mock('swiper/modules', () => ({ Autoplay: {}, Navigation: {} }))
vi.mock('swiper/css', () => ({}))
vi.mock('swiper/css/navigation', () => ({}))

/** The test id of the root element of each section the page composes. */
enum SectionTestId {
  /** The text hero, which stands in for the slider while the catalogue has no slides. */
  Hero = 'sections-hero',
  /** Performance figures. */
  Performance = 'sections-performance',
  /** Plans and product families. */
  Products = 'sections-products',
  /** Why choose us. */
  WhyChooseUs = 'sections-why-choose-us',
  /** Server locations. */
  Locations = 'sections-locations',
  /** Security protections. */
  Security = 'sections-security',
  /** One-click apps. */
  Apps = 'sections-apps',
  /** Guarantees. */
  Guarantees = 'sections-guarantees',
  /** Process. */
  Process = 'sections-process',
  /** Testimonials. */
  Testimonials = 'sections-testimonials',
  /** Support channels. */
  Support = 'sections-support',
  /** Partner logos. */
  Partners = 'sections-partners',
  /** Questions and answers. */
  Faq = 'sections-faq',
  /** Closing call to action. */
  Cta = 'sections-cta',
}

/** The sections in landing order. */
const LANDING_ORDER: readonly SectionTestId[] = Object.values(SectionTestId)

/** The sections the page composes and the header they share, by the name the template uses for each, so they resolve as Nuxt would auto-import them. */
const SECTIONS = {
  MarketingSectionHeader,
  SectionsApps,
  SectionsCTA,
  SectionsDomainSearch,
  SectionsFAQ,
  SectionsGuarantees,
  SectionsHero,
  SectionsLocations,
  SectionsPartners,
  SectionsPerformance,
  SectionsProcess,
  SectionsProducts,
  SectionsProductsFullSlider,
  SectionsProductsSlide,
  SectionsSecurity,
  SectionsSupport,
  SectionsTestimonials,
  SectionsWhyChooseUs,
}

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies, stub } = usePortalSettingsServer({
  useGuarantees,
  useHomeSections,
  useInstallerApps,
  usePerformanceStats,
  useSecurityFeatures,
  useSupportChannels,
  useTemplate,
})

/** The settings that give the sections which hide themselves while the operator has entered nothing something to show. */
const CONTENT_SETTINGS: Record<string, string> = {
  [PortalSettingKey.StatsUptime]: '99.9%',
  [PortalSettingKey.FeaturesSsl]: SETTING_ON,
  [PortalSettingKey.MigrationFree]: SETTING_ON,
}

/** More questions than the home preview shows. */
const FAQS = Array.from({ length: HOME_FAQ_LIMIT + 2 }, (_unused, index) => faqFixture(index + 1))

/**
 * The public reads the sections that stay on start on mount, which the mount waits for so no read is still on its way
 * when the stub closes.
 *
 * @param off - The sections the operator has switched off.
 * @returns The paths of the reads.
 */
const readsOf = (off: HomeSection[]): string[] => [
  CatalogPath.Slides,
  ...(off.includes(HomeSection.Testimonials) ? [] : [StorefrontPath.Testimonials]),
  ...(off.includes(HomeSection.Partners) && off.includes(HomeSection.Trusted) ? [] : [StorefrontPath.Partners]),
  ...(off.includes(HomeSection.Locations) ? [] : [StorefrontPath.Locations]),
  ...(off.includes(HomeSection.Faq) ? [] : [StorefrontPath.Faqs]),
]

/**
 * Mounts the page with these sections switched off, the rest left at their defaults, and waits until every section
 * that stays on has rendered.
 *
 * @param off - The sections the operator has switched off.
 * @returns The wrapper around the page.
 */
const mountHome = async (off: HomeSection[] = []): Promise<VueWrapper> => {
  await loadPortalSettings(replies, { ...CONTENT_SETTINGS, ...homeSectionSettings(off, SETTING_OFF) })
  const wrapper = await mountAsyncPage(ClassicHome, { components: { ...CLASSIC_COMPONENTS, ...SECTIONS, CatalogProductFamilyCard: ProductFamilyCard } })
  // Only the plans section reads the currencies and the catalogue, so only with it on is there a read to wait for.
  if (!off.includes(HomeSection.Plans)) {
    await awaitCatalogue()
  }
  for (const path of readsOf(off)) {
    await until(() => requestsTo(stub().requests, path).length > 0, `a read of ${path}`)
  }
  await until(() => wrapper.find('[data-testid="classic-home"]').exists(), 'the home page to render')
  await untilStable(() => wrapper.html(), 'the home sections to finish rendering their data')
  return wrapper
}

/**
 * Lists the sections on screen, in page order.
 *
 * @param wrapper - The mounted page.
 * @returns The test ids of the section roots, the SEO heading left out.
 */
const sectionsOn = (wrapper: VueWrapper): string[] =>
  [...wrapper.get('[data-testid="classic-home"]').element.children]
    .map((child) => child.getAttribute('data-testid') ?? '')
    .filter((testid) => (Object.values(SectionTestId) as string[]).includes(testid))

describe('ClassicHome', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
    window.localStorage.clear()
    replies[readOf(CatalogPath.Slides)] = { body: [] }
    replies[readOf(CatalogPath.BillingCurrencies)] = { body: [USD_CURRENCY] }
    replies[readOf(CatalogPath.ProductGroups)] = { body: [] }
    replies[readOf(CatalogPath.Products)] = { body: [] }
    replies[readOf(CatalogPath.ProductFeatures)] = { body: [] }
    replies[readOf(StorefrontPath.Testimonials)] = { body: [testimonialFixture(1)] }
    replies[readOf(StorefrontPath.Partners)] = { body: [partnerFixture(1)] }
    replies[readOf(StorefrontPath.Locations)] = { body: [locationFixture(1)] }
    replies[readOf(StorefrontPath.Faqs)] = { body: FAQS }
  })

  it('renders the SEO heading visually hidden', async () => {
    const wrapper = await mountHome()

    const title = wrapper.find('[data-testid="classic-home-title"]')
    expect(title.element.tagName).toBe('H1')
    expect(title.classes()).toContain('sr-only')
    expect(title.text()).toBe('seo.home.h1')
  })

  it('previews only the home limit of FAQ questions', async () => {
    const wrapper = await mountHome()

    expect(FAQS.length).toBeGreaterThan(HOME_FAQ_LIMIT)
    expect(wrapper.get(`[data-testid="${SectionTestId.Faq}"]`).findAll('[data-testid^="ui-accordion-toggle-"]')).toHaveLength(HOME_FAQ_LIMIT)
  })

  it('renders every section in landing order', async () => {
    const wrapper = await mountHome()

    expect(sectionsOn(wrapper)).toEqual(LANDING_ORDER)
  })

  it('hides a section whose toggle is off and keeps the rest', async () => {
    const wrapper = await mountHome([HomeSection.Security])

    expect(sectionsOn(wrapper)).not.toContain(SectionTestId.Security)
    expect(sectionsOn(wrapper)).toEqual(LANDING_ORDER.filter((testid) => testid !== SectionTestId.Security))
  })

  it.each<[HomeSection, SectionTestId]>([
    [HomeSection.Performance, SectionTestId.Performance],
    [HomeSection.Plans, SectionTestId.Products],
    [HomeSection.Services, SectionTestId.WhyChooseUs],
    [HomeSection.Locations, SectionTestId.Locations],
    [HomeSection.Apps, SectionTestId.Apps],
    [HomeSection.Guarantees, SectionTestId.Guarantees],
    [HomeSection.Process, SectionTestId.Process],
    [HomeSection.Testimonials, SectionTestId.Testimonials],
    [HomeSection.Support, SectionTestId.Support],
    [HomeSection.Faq, SectionTestId.Faq],
    [HomeSection.Cta, SectionTestId.Cta],
  ])('hides the %s section when its toggle is off', async (section, testid) => {
    const wrapper = await mountHome([section])

    expect(wrapper.find(`[data-testid="${testid}"]`).exists()).toBe(false)
  })

  it.each<[HomeSection[], boolean]>([
    [[], true],
    [[HomeSection.Partners], true],
    [[HomeSection.Trusted], true],
    [[HomeSection.Partners, HomeSection.Trusted], false],
  ])('shows the partners once, and hides them only when both the Partners and Trusted switches are off (%j)', async (off, shown) => {
    const wrapper = await mountHome(off)

    expect(wrapper.findAll(`[data-testid="${SectionTestId.Partners}"]`)).toHaveLength(shown ? 1 : 0)
  })

  it('always shows the hero, which has no toggle', async () => {
    const wrapper = await mountHome(Object.values(HomeSection))

    expect(wrapper.find(`[data-testid="${SectionTestId.Hero}"]`).exists()).toBe(true)
  })

  it('keeps the projects section off by default', () => {
    expect(HOME_SECTION_DEFAULT[HomeSection.Projects]).toBe(false)
    expect(useHomeSections().isVisible(HomeSection.Projects)).toBe(false)
  })
})
