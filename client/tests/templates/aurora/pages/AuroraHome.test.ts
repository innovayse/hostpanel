/**
 * Unit tests for AuroraHome: every section is composed in, the optional ones can be switched off,
 * and the search and billing events travel up to the page that owns the data.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { faqFixture, partnerFixture, testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { requestsTo, routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import AuroraHome from '~/templates/aurora/pages/AuroraHome.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { PricingCycle } from '~/types/pricingcycle'
import { stubAuroraGlobals, PLANS } from '../auroraGlobals'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route, so the FAQ and testimonials sections have content to show. */
const replies: StubRouteTable = {}

describe('AuroraHome', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    Object.assign(replies, {
      [readOf(StorefrontPath.Partners)]: { body: [] },
      [readOf(StorefrontPath.Projects)]: { body: [] },
      [readOf(StorefrontPath.Locations)]: { body: [] },
      [readOf(StorefrontPath.Faqs)]: { body: [faqFixture(1), faqFixture(2)] },
      [readOf(StorefrontPath.Testimonials)]: { body: [testimonialFixture(1), testimonialFixture(2)] },
    })
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('composes the hero, search, trust bar, plans, services, process, testimonials, FAQ and call to action', async () => {
    stubAuroraGlobals()
    const wrapper = await mountAfterReads(
      stub,
      [StorefrontPath.Partners, StorefrontPath.Locations, StorefrontPath.Testimonials, StorefrontPath.Faqs],
      () => mountWithKit(AuroraHome, { props: { plans: PLANS } }),
    )

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
    stubAuroraGlobals()
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
    const wrapper = await mountAfterReads(
      stub,
      [StorefrontPath.Partners, StorefrontPath.Locations, StorefrontPath.Testimonials],
      () => mountWithKit(AuroraHome, { props: { plans: PLANS } }),
    )

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

    replies[readOf(StorefrontPath.Partners)] = { body: [partnerFixture(1), partnerFixture(2)] }

    for (const [settings, count] of cases) {
      stubAuroraGlobals({ settings })
      // The partners are only read while their section is on; the testimonials always are.
      const reads = count > 0 ? [StorefrontPath.Partners, StorefrontPath.Testimonials] : [StorefrontPath.Testimonials]
      const wrapper = await mountAfterReads(stub, reads, () => mountWithKit(AuroraHome))

      expect(wrapper.findAll('[data-testid="aurora-partners"]')).toHaveLength(count)
      expect(wrapper.find('[data-testid="aurora-trusted"]').exists()).toBe(false)
    }
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
    const wrapper = await mountAfterReads(stub, [StorefrontPath.Projects], () => mountWithKit(AuroraHome))

    expect(wrapper.exists()).toBe(true)
    expect(requestsTo(stub.requests, StorefrontPath.Projects)).toHaveLength(1)
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
