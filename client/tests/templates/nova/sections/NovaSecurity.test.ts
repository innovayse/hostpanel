/**
 * Unit tests for NovaSecurity: a card per switched-on claim, followed by the statements that are
 * true of every install.
 *
 * @module tests/templates/nova/sections/NovaSecurity.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import NovaSecurity from '~/templates/nova/sections/NovaSecurity.vue'
import { SECURITY_FEATURES } from '~/templates/nova/content'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

describe('NovaSecurity', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubLocalePath()
    vi.stubGlobal('useSecurityFeatures', useSecurityFeatures)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders a card per switched-on claim plus the always-true statements', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.FeaturesSsl]: SETTING_ON,
      [PortalSettingKey.FeaturesDdos]: SETTING_ON,
      [PortalSettingKey.FeaturesFirewall]: SETTING_ON,
    })

    expect(mountWithKit(NovaSecurity).findAll('ul > li')).toHaveLength(3 + SECURITY_FEATURES.length)
  })

  it('states the account and isolation lines even when no claim is on', async () => {
    await loadPortalSettings(replies)
    const text = mountWithKit(NovaSecurity).text()

    expect(text).toContain('nova.security.accountTitle')
    expect(text).toContain('nova.security.isolationTitle')
    expect(text).not.toContain('nova.security.sslTitle')
  })

  it('uses the DDoS wording for the DDoS claim', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.FeaturesDdos]: SETTING_ON })

    expect(mountWithKit(NovaSecurity).text()).toContain('nova.security.ddosTitle')
  })
})
