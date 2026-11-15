/**
 * Unit tests for NovaGuarantees: a card per guarantee the operator has set, hidden when none.
 *
 * @module tests/templates/nova/sections/NovaGuarantees.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import NovaGuarantees from '~/templates/nova/sections/NovaGuarantees.vue'
import { useGuarantees } from '~/composables/useGuarantees'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useGuarantees })

describe('NovaGuarantees', () => {
  it('renders one card per guarantee', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.MigrationFree]: SETTING_ON, [PortalSettingKey.GuaranteeMoneyBackDays]: '30' })
    const wrapper = mountWithKit(NovaGuarantees)

    expect(wrapper.findAll('[data-testid="nova-guarantees-grid"] > li')).toHaveLength(2)
    expect(wrapper.find('[data-testid="nova-guarantee-money_back"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nova-guarantee-no_hidden_fees"]').exists()).toBe(false)
  })

  it('renders nothing when no guarantee is set', async () => {
    await loadPortalSettings(replies)

    expect(mountWithKit(NovaGuarantees).find('[data-testid="nova-guarantees"]').exists()).toBe(false)
  })
})
