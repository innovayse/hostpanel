/**
 * Unit tests for NovaApps: a card per installer category and a count line that appears only when set.
 *
 * @module tests/templates/nova/sections/NovaApps.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import NovaApps from '~/templates/nova/sections/NovaApps.vue'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { balancedColumns } from '~/utils/balancedGrid'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useInstallerApps })

/** The operator's installer count setting, as written. */
const COUNT_SET: Record<string, string> = { [PortalSettingKey.InstallerAppCount]: '150+' }

describe('NovaApps', () => {
  it('renders a card per category in the balanced grid', async () => {
    await loadPortalSettings(replies, COUNT_SET)
    const wrapper = mountWithKit(NovaApps)
    const grid = wrapper.find('[data-testid="nova-apps-grid"]')

    expect(grid.findAll('li')).toHaveLength(Object.values(InstallerAppCategory).length)
    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(6)))
  })

  it('shows the count line when the count is set', async () => {
    await loadPortalSettings(replies, COUNT_SET)

    expect(mountWithKit(NovaApps).find('[data-testid="nova-apps-count"]').exists()).toBe(true)
  })

  it('hides the count line when the count is empty but keeps the categories', async () => {
    await loadPortalSettings(replies)
    const wrapper = mountWithKit(NovaApps)

    expect(wrapper.find('[data-testid="nova-apps-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nova-apps-grid"]').exists()).toBe(true)
  })
})
