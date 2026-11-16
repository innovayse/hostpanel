/**
 * Tests for `useBrandLogo`: the light/dark/mark rule over the operator's uploaded images.
 *
 * @module tests/composables/useBrandLogo.test
 */

import { describe, expect, it } from 'vitest'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { ColorMode } from '~/types/colormode'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useAppColorMode, useSiteIdentity })

/** The operator's two wordmarks, one for each background. */
const BOTH_LOGOS: Record<string, string> = { [PortalSettingKey.Logo]: '/l.png', [PortalSettingKey.LogoDark]: '/d.png' }

describe('useBrandLogo', () => {
  it('reports nothing custom when no logo was uploaded', async () => {
    await loadPortalSettings(replies)

    const logo = useBrandLogo()

    expect(logo.isCustom.value).toBe(false)
    expect(logo.light.value).toBe('')
    expect(logo.dark.value).toBe('')
    expect(logo.mark.value).toBe('')
    expect(logo.hasDarkVariant.value).toBe(false)
  })

  it('uses the site name as alt text', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.SiteName]: 'Acme' })

    expect(useBrandLogo().alt.value).toBe('Acme')
  })

  it('shows a single logo in both modes when only the light one exists', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.Logo]: '/l.png' })

    const logo = useBrandLogo()

    expect(logo.isCustom.value).toBe(true)
    expect(logo.light.value).toBe('/l.png')
    expect(logo.dark.value).toBe('/l.png')
    expect(logo.hasDarkVariant.value).toBe(false)
  })

  it('exposes both wordmarks when they differ', async () => {
    await loadPortalSettings(replies, BOTH_LOGOS)

    const logo = useBrandLogo()

    expect(logo.hasDarkVariant.value).toBe(true)
    expect(logo.logoOnDark.value).toBe('/d.png')
  })

  it('picks the dark logo while the page is in dark mode', async () => {
    await loadPortalSettings(replies, { ...BOTH_LOGOS, [PortalSettingKey.ThemeDefault]: ColorMode.Dark })

    expect(useBrandLogo().logo.value).toBe('/d.png')
  })

  it('picks the light logo while the page is in light mode', async () => {
    await loadPortalSettings(replies, { ...BOTH_LOGOS, [PortalSettingKey.ThemeDefault]: ColorMode.Light })

    expect(useBrandLogo().logo.value).toBe('/l.png')
  })

  it('prefers the uploaded mark', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.Logo]: '/l.png', [PortalSettingKey.LogoMark]: '/m.png' })

    expect(useBrandLogo().mark.value).toBe('/m.png')
  })

  it('falls back to the wordmark when no mark is uploaded', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.Logo]: '/l.png', [PortalSettingKey.ThemeDefault]: ColorMode.Light })

    expect(useBrandLogo().mark.value).toBe('/l.png')
  })
})
