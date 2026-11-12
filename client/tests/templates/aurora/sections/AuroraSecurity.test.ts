/**
 * Unit tests for AuroraSecurity: one card per protection the operator switched on and nothing without any.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraSecurity from '~/templates/aurora/sections/AuroraSecurity.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

describe('AuroraSecurity', () => {
  it('renders a card for each feature that is on and none for the others', () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.FeaturesSsl]: SETTING_ON, [PortalSettingKey.FeaturesDdos]: SETTING_ON } })

    const wrapper = mountWithKit(AuroraSecurity)

    expect(wrapper.find('[data-testid="security-ssl"]').text()).toContain('aurora.security.sslTitle')
    expect(wrapper.find('[data-testid="security-ddos"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="security-firewall"]').exists()).toBe(false)
  })

  it('renders nothing when no feature is on', () => {
    stubAuroraGlobals()

    expect(mountWithKit(AuroraSecurity).find('[data-testid="aurora-security"]').exists()).toBe(false)
  })

  it('lays the cards out in the balanced grid', () => {
    stubAuroraGlobals({
      settings: {
        [PortalSettingKey.FeaturesSsl]: SETTING_ON,
        [PortalSettingKey.FeaturesBackups]: SETTING_ON,
        [PortalSettingKey.FeaturesDdos]: SETTING_ON,
        [PortalSettingKey.FeaturesMalware]: SETTING_ON,
        [PortalSettingKey.FeaturesFirewall]: SETTING_ON,
      },
    })

    const grid = mountWithKit(AuroraSecurity).find('[data-testid="aurora-security-grid"]')

    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(5)))
  })
})
