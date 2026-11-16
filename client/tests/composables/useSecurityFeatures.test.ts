/**
 * Tests for `useSecurityFeatures`: a protection shows only when its switch is on.
 *
 * @module tests/composables/useSecurityFeatures.test
 */

import { describe, expect, it } from 'vitest'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useSecurityFeatures', () => {
  it('is empty when nothing is switched on', async () => {
    await loadPortalSettings(replies)

    expect(useSecurityFeatures().value).toEqual([])
  })

  it('returns the switched-on protections in enum order and skips a switched-off one', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.FeaturesFirewall]: SETTING_ON,
      [PortalSettingKey.FeaturesSsl]: SETTING_ON,
      [PortalSettingKey.FeaturesDdos]: SETTING_OFF,
      [PortalSettingKey.FeaturesBackups]: SETTING_ON,
    })

    expect(useSecurityFeatures().value).toEqual([
      SecurityFeatureKey.Ssl,
      SecurityFeatureKey.Backups,
      SecurityFeatureKey.Firewall,
    ])
  })
})
