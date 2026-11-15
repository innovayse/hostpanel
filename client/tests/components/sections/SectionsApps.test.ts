/**
 * Unit tests for SectionsApps: the fixed categories always show, the count line only when the
 * operator set a count.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import SectionsApps from '~/components/sections/SectionsApps.vue'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useInstallerApps })

/** The operator's installer count setting, as written. */
const COUNT_SET: Record<string, string> = { [PortalSettingKey.InstallerAppCount]: '150+' }

describe('SectionsApps', () => {
  it('shows one card per category', async () => {
    await loadPortalSettings(replies, COUNT_SET)

    const wrapper = mountWithKit(SectionsApps, { components: CLASSIC_COMPONENTS })

    for (const category of Object.values(InstallerAppCategory)) {
      expect(wrapper.find(`[data-testid="app-${category}"]`).exists()).toBe(true)
    }
  })

  it('shows the count line when a count is set', async () => {
    await loadPortalSettings(replies, COUNT_SET)

    const wrapper = mountWithKit(SectionsApps, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-apps-count"]').exists()).toBe(true)
  })

  it('hides the count line when the count is empty but keeps the section', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(SectionsApps, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-apps-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sections-apps"]').exists()).toBe(true)
  })

  it('draws every icon on a flat brand tile with no accent glow', async () => {
    await loadPortalSettings(replies, COUNT_SET)

    const wrapper = mountWithKit(SectionsApps, { components: CLASSIC_COMPONENTS })

    const tiles = wrapper.findAll('[data-testid="classic-icon-tile"]')
    expect(tiles.length).toBeGreaterThan(0)
    expect(wrapper.html()).not.toContain('--accent')
  })
})
