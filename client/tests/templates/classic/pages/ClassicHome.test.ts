/**
 * Unit tests for ClassicHome: the hidden H1, the marketing sections in hosting-site order, and
 * the per-section visibility toggles.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { useHomeSections } from '~/composables/useHomeSections'
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { HOME_SECTION_DEFAULT, HOME_SECTION_SETTING, HomeSection } from '~/constants/homeSection'
import ClassicHome from '~/templates/classic/pages/ClassicHome.vue'

/** Names of the section components the home page composes, in the order they appear. */
const SECTION_NAMES = [
  'SectionsProductsFullSlider',
  'SectionsPerformance',
  'SectionsProducts',
  'SectionsWhyChooseUs',
  'SectionsLocations',
  'SectionsSecurity',
  'SectionsApps',
  'SectionsGuarantees',
  'SectionsProcess',
  'SectionsTestimonials',
  'SectionsSupport',
  'SectionsPartners',
  'SectionsFAQ',
  'SectionsCTA',
]

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

/** What the operator writes into a section's switch to turn it off. */
const SWITCHED_OFF = 'false'

/**
 * Loads the operator's settings with these sections switched off, the rest left at their defaults.
 *
 * @param off The sections the operator has switched off.
 */
const switchOff = (off: Iterable<HomeSection>): Promise<void> =>
  loadPortalSettings(replies, Object.fromEntries([...off].map((section) => [HOME_SECTION_SETTING[section], SWITCHED_OFF])))

/**
 * Builds a stand-in section that renders its own name, so order can be read from the DOM.
 *
 * @param name The section component's registered name.
 * @returns The stub component.
 */
const sectionStub = (name: string) => defineComponent({
  name,
  props: { limit: { type: Number, default: undefined } },
  setup: (props) => () => h('section', { 'data-section': name, 'data-limit': props.limit === undefined ? undefined : String(props.limit) }),
})

describe('ClassicHome', () => {
  const components = Object.fromEntries(SECTION_NAMES.map((name) => [name, sectionStub(name)]))

  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    vi.stubGlobal('useHomeSections', useHomeSections)
    await switchOff([])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders the SEO heading visually hidden', () => {
    const wrapper = mountWithKit(ClassicHome, { components })

    const title = wrapper.find('[data-testid="classic-home-title"]')
    expect(title.element.tagName).toBe('H1')
    expect(title.classes()).toContain('sr-only')
    expect(title.text()).toBe('seo.home.h1')
  })

  it('previews only the home limit of FAQ questions', () => {
    const wrapper = mountWithKit(ClassicHome, { components })

    expect(wrapper.find('[data-section="SectionsFAQ"]').attributes('data-limit')).toBe(String(HOME_FAQ_LIMIT))
  })

  it('renders every section in landing order', () => {
    const wrapper = mountWithKit(ClassicHome, { components })

    const order = wrapper.findAll('[data-section]').map((node) => node.attributes('data-section'))
    expect(order).toEqual(SECTION_NAMES)
  })

  it('hides a section whose toggle is off and keeps the rest', async () => {
    await switchOff([HomeSection.Security])

    const wrapper = mountWithKit(ClassicHome, { components })

    const order = wrapper.findAll('[data-section]').map((node) => node.attributes('data-section'))
    expect(order).not.toContain('SectionsSecurity')
    expect(order).toEqual(SECTION_NAMES.filter((name) => name !== 'SectionsSecurity'))
  })

  it('hides each toggleable section when its toggle is off', async () => {
    const toggled: Array<[HomeSection, string]> = [
      [HomeSection.Performance, 'SectionsPerformance'],
      [HomeSection.Plans, 'SectionsProducts'],
      [HomeSection.Services, 'SectionsWhyChooseUs'],
      [HomeSection.Locations, 'SectionsLocations'],
      [HomeSection.Apps, 'SectionsApps'],
      [HomeSection.Guarantees, 'SectionsGuarantees'],
      [HomeSection.Process, 'SectionsProcess'],
      [HomeSection.Testimonials, 'SectionsTestimonials'],
      [HomeSection.Support, 'SectionsSupport'],
      [HomeSection.Faq, 'SectionsFAQ'],
      [HomeSection.Cta, 'SectionsCTA'],
    ]

    for (const [section, name] of toggled) {
      await switchOff([section])

      const wrapper = mountWithKit(ClassicHome, { components })

      expect(wrapper.find(`[data-section="${name}"]`).exists()).toBe(false)
    }
  })

  it('shows the partners once, and hides them only when both the Partners and Trusted switches are off', async () => {
    const cases: Array<[HomeSection[], boolean]> = [
      [[], true],
      [[HomeSection.Partners], true],
      [[HomeSection.Trusted], true],
      [[HomeSection.Partners, HomeSection.Trusted], false],
    ]

    for (const [off, shown] of cases) {
      await switchOff(off)

      const wrapper = mountWithKit(ClassicHome, { components })

      expect(wrapper.findAll('[data-section="SectionsPartners"]')).toHaveLength(shown ? 1 : 0)
      expect(wrapper.find('[data-section="SectionsTrusted"]').exists()).toBe(false)
    }
  })

  it('always shows the hero, which has no toggle', async () => {
    await switchOff(Object.values(HomeSection))

    const wrapper = mountWithKit(ClassicHome, { components })

    expect(wrapper.find('[data-section="SectionsProductsFullSlider"]').exists()).toBe(true)
  })

  it('keeps the projects section off by default', () => {
    expect(HOME_SECTION_DEFAULT[HomeSection.Projects]).toBe(false)
    expect(useHomeSections().isVisible(HomeSection.Projects)).toBe(false)
  })
})
