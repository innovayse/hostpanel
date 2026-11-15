/**
 * Unit tests for NovaHome: the page root the header's skip link points at, and its sections.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { homeSectionSettings, loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { partnerFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { readOf } from '~/tests/helpers/stubRoutes'
import NovaHome from '~/templates/nova/pages/NovaHome.vue'
import { useGuarantees } from '~/composables/useGuarantees'
import { useHomeSections } from '~/composables/useHomeSections'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { usePerformanceStats } from '~/composables/usePerformanceStats'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { HomeSection } from '~/constants/homeSection'
import { PLANS } from '~/tests/templates/aurora/auroraGlobals'
import { PricingCycle } from '~/types/pricingcycle'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'

/**
 * The real composables the sections call, registered under the auto-import names Nuxt gives them. Registering the
 * real composable under its own name replaces nothing, which is why the `noOwnMocks` guard allows it.
 */
const REAL_COMPOSABLES = { useGuarantees, useHomeSections, useInstallerApps, usePerformanceStats, useSecurityFeatures, useSiteIdentity, useSupportChannels }

/** The stub playing the BFF, answering the storefront reads and the operator's settings, and what it answers, by route. */
const { replies, stub } = usePortalSettingsServer(REAL_COMPOSABLES)

/**
 * Makes the operator's settings fill every data-gated section the page composes (a performance figure, a
 * security claim, a guarantee), with every home section on unless a test hides some.
 *
 * @param hidden The sections the operator has switched off.
 * @returns Resolves once the settings are loaded into the shared state.
 */
const serveSettings = (hidden: HomeSection[] = []): Promise<void> =>
  loadPortalSettings(replies, {
    [PortalSettingKey.StatsUptime]: '99.9%',
    [PortalSettingKey.FeaturesSsl]: SETTING_ON,
    [PortalSettingKey.GuaranteeNoHiddenFees]: SETTING_ON,
    ...homeSectionSettings(Object.values(HomeSection), SETTING_ON),
    ...homeSectionSettings(hidden, SETTING_OFF),
  })

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
    for (const path of Object.values(StorefrontPath)) {
      replies[readOf(path)] = { body: [] }
    }
    replies[readOf(StorefrontPath.Partners)] = { body: [partnerFixture(1), partnerFixture(2)] }
    await serveSettings()
  })

  it('is the skip-link target and carries the nova palette', () => {
    const wrapper = mountWithKit(NovaHome)

    expect(wrapper.attributes('id')).toBe('nova-main')
    expect(wrapper.attributes('data-testid')).toBe('nova-home')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['tpl-nova', 'bg-nova-bg']))
  })

  it('owns exactly one h1, in the hero', () => {
    expect(mountWithKit(NovaHome).findAll('h1')).toHaveLength(1)
  })

  it('shows the empty plans state when no plans arrive', () => {
    expect(mountWithKit(NovaHome).find('[data-testid="nova-plans-empty"]').exists()).toBe(true)
  })

  it('passes the billing choice up unchanged', async () => {
    const wrapper = mountWithKit(NovaHome, { props: { plans: PLANS } })

    await wrapper.find('[data-testid="nova-billing-annual"]').trigger('click')

    expect(wrapper.emitted('update:cycle')).toEqual([[PricingCycle.Annual]])
  })

  it('does not leak the declared domain-search props onto the root as attributes', () => {
    const wrapper = mountWithKit(NovaHome, { props: { domainPending: true, hasZones: false } })

    expect(wrapper.attributes('domainpending')).toBeUndefined()
    expect(wrapper.attributes('haszones')).toBeUndefined()
  })

  it('renders every data-free section while all are switched on', () => {
    const wrapper = mountWithKit(NovaHome)

    for (const testId of Object.values(SECTION_TEST_IDS)) {
      expect(wrapper.find(`[data-testid="${testId}"]`).exists(), testId).toBe(true)
    }
  })

  it.each(Object.entries(SECTION_TEST_IDS) as [HomeSection, string][])('drops the %s section when the operator hides it', async (section, testId) => {
    await serveSettings([section])

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
      await serveSettings(hidden)
      // With both switches off the partners section is never mounted, so no partners read is started.
      const wrapper = await mountAfterReads(stub(), count > 0 ? [StorefrontPath.Partners] : [], () => mountWithKit(NovaHome))

      expect(wrapper.findAll('[data-testid="nova-partners"]')).toHaveLength(count)
      expect(wrapper.find('[data-testid="nova-trusted"]').exists()).toBe(false)
    }
  })

  it('keeps the hero when every section is hidden', async () => {
    await serveSettings(Object.values(HomeSection))
    const wrapper = mountWithKit(NovaHome)

    expect(wrapper.findAll('h1')).toHaveLength(1)
    for (const testId of Object.values(SECTION_TEST_IDS)) {
      expect(wrapper.find(`[data-testid="${testId}"]`).exists(), testId).toBe(false)
    }
  })
})
