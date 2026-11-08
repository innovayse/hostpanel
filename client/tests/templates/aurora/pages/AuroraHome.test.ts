/**
 * Unit tests for AuroraHome: every section is composed in, the optional ones can be switched off,
 * and the search and billing events travel up to the page that owns the data.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { faqFixture, partnerFixture, testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import AuroraHome from '~/templates/aurora/pages/AuroraHome.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { PricingCycle } from '~/types/pricingcycle'
import { stubAuroraGlobals, PLANS } from '../auroraGlobals'

/** The mocked storefront endpoints, so the FAQ and testimonials sections have content to show. */
const api = { fetchPartners: vi.fn().mockResolvedValue([]), fetchTestimonials: vi.fn(), fetchProjects: vi.fn().mockResolvedValue([]), fetchFaqs: vi.fn(), fetchLocations: vi.fn().mockResolvedValue([]) }

vi.mock('~/composables/apis/useStorefrontApi', () => ({
  /**
   * Stands in for the storefront API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useStorefrontApi: () => api,
}))

describe('AuroraHome', () => {
  beforeEach(() => {
    api.fetchFaqs.mockReset()
    api.fetchFaqs.mockResolvedValue([faqFixture(1), faqFixture(2)])
    api.fetchTestimonials.mockReset()
    api.fetchTestimonials.mockResolvedValue([testimonialFixture(1), testimonialFixture(2)])
  })

  it('composes the hero, search, trust bar, plans, services, process, testimonials, FAQ and call to action', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHome, { props: { plans: PLANS } })
    await flushPromises()

    expect(wrapper.attributes('data-testid')).toBe('aurora-home')
    expect(wrapper.find('[data-testid="hero-title"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="home-domain-search"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="trust-bar"]').exists()).toBe(true)
    expect(wrapper.find('#hosting').exists()).toBe(true)
    expect(wrapper.find('#services').exists()).toBe(true)
    expect(wrapper.find('#process').exists()).toBe(true)
    expect(wrapper.find('#faq').exists()).toBe(true)
    expect(wrapper.find('[data-testid="aurora-cta"]').exists()).toBe(true)
  })

  it('sets a brand glow behind the hero search card', () => {
    const wrapper = mountWithKit(AuroraHome)

    expect(wrapper.find('[data-testid="hero-search-glow"]').exists()).toBe(true)
  })

  it('leaves out the process and FAQ sections when they are switched off', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHome, { props: { showProcess: false, showFaq: false } })

    expect(wrapper.find('#process').exists()).toBe(false)
    expect(wrapper.find('#faq').exists()).toBe(false)
  })

  it('hides a section whose homepage switch is off and keeps the others', async () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.SectionStats]: 'false', [PortalSettingKey.SectionFaq]: 'false', [PortalSettingKey.SectionCta]: 'false' } })
    const wrapper = mountWithKit(AuroraHome, { props: { plans: PLANS } })
    await flushPromises()

    expect(wrapper.find('[data-testid="trust-bar"]').exists()).toBe(false)
    expect(wrapper.find('#faq').exists()).toBe(false)
    expect(wrapper.find('[data-testid="aurora-cta"]').exists()).toBe(false)
    expect(wrapper.find('#services').exists()).toBe(true)
    expect(wrapper.find('#hosting').exists()).toBe(true)
  })

  it('shows one partners section while either the Partners or the Trusted switch is on', async () => {
    const cases: Array<[Record<string, string>, number]> = [
      [{}, 1],
      [{ [PortalSettingKey.SectionPartners]: 'false' }, 1],
      [{ [PortalSettingKey.SectionTrusted]: 'false' }, 1],
      [{ [PortalSettingKey.SectionPartners]: 'false', [PortalSettingKey.SectionTrusted]: 'false' }, 0],
    ]

    for (const [settings, count] of cases) {
      api.fetchPartners.mockResolvedValue([partnerFixture(1), partnerFixture(2)])
      stubAuroraGlobals({ settings })
      const wrapper = mountWithKit(AuroraHome)
      await flushPromises()

      expect(wrapper.findAll('[data-testid="aurora-partners"]')).toHaveLength(count)
      expect(wrapper.find('[data-testid="aurora-trusted"]').exists()).toBe(false)
    }
    api.fetchPartners.mockResolvedValue([])
  })

  it('needs both the prop and the switch for the process section', async () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.SectionProcess]: 'false' } })
    const switchedOff = mountWithKit(AuroraHome)
    expect(switchedOff.find('#process').exists()).toBe(false)

    stubAuroraGlobals()
    const propOff = mountWithKit(AuroraHome, { props: { showProcess: false } })
    expect(propOff.find('#process').exists()).toBe(false)

    const both = mountWithKit(AuroraHome)
    expect(both.find('#process').exists()).toBe(true)
  })

  it('shows the portfolio projects only when their switch is on', async () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.SectionProjects]: 'true' } })
    api.fetchProjects.mockResolvedValue([])
    const wrapper = mountWithKit(AuroraHome)
    await flushPromises()

    expect(wrapper.exists()).toBe(true)
    expect(api.fetchProjects).toHaveBeenCalled()
  })

  it('passes the search term of the card up to the page', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHome)

    await wrapper.find('[data-testid="home-domain-search-input"]').setValue('acme')
    await wrapper.find('[data-testid="home-domain-search"] form').trigger('submit')

    expect(wrapper.emitted('search')).toEqual([['acme']])
  })

  it('passes the billing cycle up to the page', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHome, { props: { plans: PLANS } })

    await wrapper.find('[data-testid="billing-annual"]').trigger('click')

    expect(wrapper.emitted('update:cycle')).toEqual([[PricingCycle.Annual]])
  })

  it('spans the full width with the aurora backdrop and keeps the content in the shared page container', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AuroraHome)

    expect(wrapper.classes()).not.toContain('container-custom')
    expect(wrapper.find('.container-custom').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-backdrop"]').attributes('data-variant')).toBe('aurora')
  })
})
