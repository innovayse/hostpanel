/**
 * Unit tests for AuroraGuarantees: a card per promise the operator's settings switch on and nothing without any.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraGuarantees from '~/templates/aurora/sections/AuroraGuarantees.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

describe('AuroraGuarantees', () => {
  it('renders a card for each promise that is set', () => {
    stubAuroraGlobals({
      settings: { [PortalSettingKey.MigrationFree]: SETTING_ON, [PortalSettingKey.GuaranteeMoneyBackDays]: '30' },
    })

    const wrapper = mountWithKit(AuroraGuarantees)

    expect(wrapper.find('[data-testid="guarantee-free_migration"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="guarantee-money_back"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="guarantee-no_hidden_fees"]').exists()).toBe(false)
  })

  it('renders nothing when no promise is set', () => {
    stubAuroraGlobals()

    expect(mountWithKit(AuroraGuarantees).find('[data-testid="aurora-guarantees"]').exists()).toBe(false)
  })

  it('lays the cards out in the balanced grid', () => {
    stubAuroraGlobals({
      settings: {
        [PortalSettingKey.MigrationFree]: SETTING_ON,
        [PortalSettingKey.GuaranteeMoneyBackDays]: '14',
        [PortalSettingKey.GuaranteeNoHiddenFees]: SETTING_ON,
      },
    })

    const grid = mountWithKit(AuroraGuarantees).find('[data-testid="aurora-guarantees-grid"]')

    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(3)))
  })
})
