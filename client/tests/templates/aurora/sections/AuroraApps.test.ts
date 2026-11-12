/**
 * Unit tests for AuroraApps: a card per installer category, the count line only when a count is set.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraApps from '~/templates/aurora/sections/AuroraApps.vue'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

describe('AuroraApps', () => {
  it('renders a card for every category and the count line when a count is set', () => {
    stubAuroraGlobals({ settings: { [PortalSettingKey.InstallerAppCount]: '150+' } })

    const wrapper = mountWithKit(AuroraApps)

    for (const category of Object.values(InstallerAppCategory)) {
      expect(wrapper.find(`[data-testid="apps-category-${category}"]`).exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="apps-count"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="aurora-apps-grid"]').attributes('data-columns')).toBe(String(balancedColumns(6)))
  })

  it('hides the count line when no count is set', () => {
    stubAuroraGlobals()

    const wrapper = mountWithKit(AuroraApps)

    expect(wrapper.find('[data-testid="aurora-apps"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="apps-count"]').exists()).toBe(false)
  })
})
