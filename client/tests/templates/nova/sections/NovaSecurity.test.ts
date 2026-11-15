/**
 * Unit tests for NovaSecurity: a card per switched-on claim, followed by the statements that are
 * true of every install.
 *
 * @module tests/templates/nova/sections/NovaSecurity.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import NovaSecurity from '~/templates/nova/sections/NovaSecurity.vue'
import { SECURITY_FEATURES } from '~/templates/nova/content'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useSecurityFeatures })

describe('NovaSecurity', () => {
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
