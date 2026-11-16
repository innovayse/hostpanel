/**
 * Tests for `useInstallerApps`: the count comes from the setting, the categories are fixed.
 *
 * @module tests/composables/useInstallerApps.test
 */

import { describe, expect, it } from 'vitest'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useInstallerApps', () => {
  it('reads the count as the operator wrote it', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.InstallerAppCount]: '150+' })

    expect(useInstallerApps().value.count).toBe('150+')
  })

  it('has an empty count when unset, but still every category', async () => {
    await loadPortalSettings(replies)

    const apps = useInstallerApps().value

    expect(apps.count).toBe('')
    expect(apps.categories).toEqual(Object.values(InstallerAppCategory))
  })
})
