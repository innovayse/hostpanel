/**
 * Tests for `useSecurityFeatures`: a protection shows only when its switch is on.
 *
 * @module tests/composables/useSecurityFeatures.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'
import { SecurityFeatureKey } from '~/constants/securityFeatureKey'
import { useSecurityFeatures } from '~/composables/useSecurityFeatures'

/**
 * Stubs the settings reader with a fixed set of values.
 *
 * @param values Setting key to value.
 */
const stubSettings = (values: Record<string, string>): void => {
  vi.stubGlobal('usePortalSettings', () => ({ get: (key: string) => values[key] ?? '' }))
}

describe('useSecurityFeatures', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is empty when nothing is switched on', () => {
    stubSettings({})

    expect(useSecurityFeatures().value).toEqual([])
  })

  it('returns the switched-on protections in enum order and skips a switched-off one', () => {
    stubSettings({
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
