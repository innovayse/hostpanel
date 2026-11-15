/**
 * Unit tests for SectionsSecurity: one card per enabled protection and no section when none is
 * enabled.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsSecurity from '~/components/sections/SectionsSecurity.vue'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

/** The SSL and firewall protections switched on; the others are left off. */
const ON_FEATURES: Record<string, string> = {
  [PortalSettingKey.FeaturesSsl]: SETTING_ON,
  [PortalSettingKey.FeaturesFirewall]: SETTING_ON,
}

describe('SectionsSecurity', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    vi.stubGlobal('useSecurityFeatures', useSecurityFeatures)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows a card with title and description for each enabled protection', async () => {
    await loadPortalSettings(replies, ON_FEATURES)

    const wrapper = mountWithKit(SectionsSecurity, { components: CLASSIC_COMPONENTS })

    const card = wrapper.find('[data-testid="security-ssl"]')
    expect(card.text()).toContain('hostingSections.security.features.ssl.title')
    expect(card.text()).toContain('hostingSections.security.features.ssl.description')
    expect(wrapper.find('[data-testid="security-firewall"]').exists()).toBe(true)
  })

  it('leaves out a protection that is not enabled', async () => {
    await loadPortalSettings(replies, ON_FEATURES)

    const wrapper = mountWithKit(SectionsSecurity, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="security-ddos"]').exists()).toBe(false)
  })

  it('renders nothing when no protection is enabled', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(SectionsSecurity, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-security"]').exists()).toBe(false)
  })

  it('draws every icon on a flat brand tile with no accent glow', async () => {
    await loadPortalSettings(replies, ON_FEATURES)

    const wrapper = mountWithKit(SectionsSecurity, { components: CLASSIC_COMPONENTS })

    const tiles = wrapper.findAll('[data-testid="classic-icon-tile"]')
    expect(tiles.length).toBeGreaterThan(0)
    expect(wrapper.html()).not.toContain('--accent')
  })

  it('has an icon for the DDoS protection', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.FeaturesDdos]: SETTING_ON })

    const wrapper = mountWithKit(SectionsSecurity, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="security-ddos"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="security-ddos"] [data-testid="classic-icon-tile"]').exists()).toBe(true)
  })
})
