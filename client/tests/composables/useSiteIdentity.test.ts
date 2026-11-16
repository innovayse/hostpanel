/**
 * Tests for `useSiteIdentity`: the operator's site name and tagline, with the brand as fallback.
 *
 * @module tests/composables/useSiteIdentity.test
 */

import { describe, expect, it } from 'vitest'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useSiteIdentity', () => {
  it('uses the name and tagline the operator set', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.SiteName]: 'Acme Hosting',
      [PortalSettingKey.SiteTagline]: 'Fast and calm',
    })

    const { name, tagline } = useSiteIdentity()

    expect(name.value).toBe('Acme Hosting')
    expect(tagline.value).toBe('Fast and calm')
  })

  it('falls back to the neutral product name and an empty tagline', async () => {
    await loadPortalSettings(replies)

    const { name, tagline } = useSiteIdentity()

    expect(name.value).toBe('Hostpanel')
    expect(tagline.value).toBe('')
  })
})
