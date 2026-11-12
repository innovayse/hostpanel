/**
 * Unit tests for NovaHome: the page root the header's skip link points at, and its sections.
 */
import { computed } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { partnerFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import NovaHome from '~/templates/nova/pages/NovaHome.vue'
import { stubTemplateGlobals } from '~/tests/helpers/templateGlobals'
import { HomeSection } from '~/constants/homeSection'
import { PLANS } from '~/tests/templates/aurora/auroraGlobals'
import { PricingCycle } from '~/types/pricingcycle'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { SupportChannel } from '~/constants/supportChannel'
import { GuaranteeKey } from '~/constants/guaranteeKey'
import { PerformanceStatKey } from '~/constants/performanceStatKey'
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route: every storefront read is empty except the partners. */
const replies: StubRouteTable = {}

/**
 * Stands in for the section composables the page's sections call, with every section on unless
 * a test hides some.
 *
 * @param hidden The sections the operator has switched off.
 */
const stubSections = (hidden: HomeSection[] = []): void => {
  vi.stubGlobal('useHomeSections', () => ({
    isVisible: (section: HomeSection) => !hidden.includes(section),
  }))
  vi.stubGlobal('usePerformanceStats', () => computed(() => [{ key: PerformanceStatKey.Uptime, value: '99.9%' }]))
  vi.stubGlobal('useSecurityFeatures', () => computed(() => [SecurityFeatureKey.Ssl]))
  vi.stubGlobal('useGuarantees', () => computed(() => [{ key: GuaranteeKey.NoHiddenFees }]))
  vi.stubGlobal('useSupportChannels', () => computed(() => [{ channel: SupportChannel.Ticket }]))
  vi.stubGlobal('useInstallerApps', () => computed(() => ({ count: '150+', categories: Object.values(InstallerAppCategory) })))
}

/** Test ids of the sections that render without any fetched data, by the section that gates them. */
const SECTION_TEST_IDS: Partial<Record<HomeSection, string>> = {
  [HomeSection.Performance]: 'nova-performance',
  [HomeSection.Security]: 'nova-security',
  [HomeSection.Apps]: 'nova-apps',
  [HomeSection.Guarantees]: 'nova-guarantees',
  [HomeSection.Support]: 'nova-support',
  [HomeSection.Plans]: 'nova-plans-empty',
}

describe('NovaHome', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    for (const path of Object.values(StorefrontPath)) {
      replies[readOf(path)] = { body: [] }
    }
    replies[readOf(StorefrontPath.Partners)] = { body: [partnerFixture(1), partnerFixture(2)] }
    stubSections()
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('is the skip-link target and carries the nova palette', () => {
    stubTemplateGlobals()
    const wrapper = mountWithKit(NovaHome)

    expect(wrapper.attributes('id')).toBe('nova-main')
    expect(wrapper.attributes('data-testid')).toBe('nova-home')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['tpl-nova', 'bg-nova-bg']))
  })

  it('owns exactly one h1, in the hero', () => {
    stubTemplateGlobals()

    expect(mountWithKit(NovaHome).findAll('h1')).toHaveLength(1)
  })

  it('shows the empty plans state when no plans arrive', () => {
    stubTemplateGlobals()

    expect(mountWithKit(NovaHome).find('[data-testid="nova-plans-empty"]').exists()).toBe(true)
  })

  it('passes the billing choice up unchanged', async () => {
    stubTemplateGlobals()
    const wrapper = mountWithKit(NovaHome, { props: { plans: PLANS } })

    await wrapper.find('[data-testid="nova-billing-annual"]').trigger('click')

    expect(wrapper.emitted('update:cycle')).toEqual([[PricingCycle.Annual]])
  })

  it('does not leak the declared domain-search props onto the root as attributes', () => {
    stubTemplateGlobals()
    const wrapper = mountWithKit(NovaHome, { props: { domainPending: true, hasZones: false } })

    expect(wrapper.attributes('domainpending')).toBeUndefined()
    expect(wrapper.attributes('haszones')).toBeUndefined()
  })

  it('renders every data-free section while all are switched on', () => {
    stubTemplateGlobals()
    const wrapper = mountWithKit(NovaHome)

    for (const testId of Object.values(SECTION_TEST_IDS)) {
      expect(wrapper.find(`[data-testid="${testId}"]`).exists(), testId).toBe(true)
    }
  })

  it.each(Object.entries(SECTION_TEST_IDS) as [HomeSection, string][])('drops the %s section when the operator hides it', (section, testId) => {
    stubTemplateGlobals()
    stubSections([section])

    expect(mountWithKit(NovaHome).find(`[data-testid="${testId}"]`).exists()).toBe(false)
  })

  it('shows one partners section while either the Partners or the Trusted switch is on', async () => {
    const cases: Array<[HomeSection[], number]> = [
      [[], 1],
      [[HomeSection.Partners], 1],
      [[HomeSection.Trusted], 1],
      [[HomeSection.Partners, HomeSection.Trusted], 0],
    ]

    for (const [hidden, count] of cases) {
      stubTemplateGlobals()
      stubSections(hidden)
      // With both switches off the partners section is never mounted, so no partners read is started.
      const wrapper = await mountAfterReads(stub, count > 0 ? [StorefrontPath.Partners] : [], () => mountWithKit(NovaHome))

      expect(wrapper.findAll('[data-testid="nova-partners"]')).toHaveLength(count)
      expect(wrapper.find('[data-testid="nova-trusted"]').exists()).toBe(false)
    }
  })

  it('keeps the hero when every section is hidden', () => {
    stubTemplateGlobals()
    stubSections(Object.values(HomeSection))
    const wrapper = mountWithKit(NovaHome)

    expect(wrapper.findAll('h1')).toHaveLength(1)
    for (const testId of Object.values(SECTION_TEST_IDS)) {
      expect(wrapper.find(`[data-testid="${testId}"]`).exists(), testId).toBe(false)
    }
  })
})
