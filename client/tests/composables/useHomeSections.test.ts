/**
 * Tests for `useHomeSections`: an explicit switch wins, a missing one falls back to the
 * section default.
 *
 * @module tests/composables/useHomeSections.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { HomeSection } from '~/constants/homeSection'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'
import { useHomeSections } from '~/composables/useHomeSections'

/**
 * Stubs the settings reader with a fixed set of values.
 *
 * @param values Setting key to value.
 */
const stubSettings = (values: Record<string, string>): void => {
  vi.stubGlobal('usePortalSettings', () => ({ get: (key: string) => values[key] ?? '' }))
}

describe('useHomeSections', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows every section but projects when nothing is set', () => {
    stubSettings({})
    const { isVisible } = useHomeSections()

    expect(isVisible(HomeSection.Faq)).toBe(true)
    expect(isVisible(HomeSection.Locations)).toBe(true)
    expect(isVisible(HomeSection.Projects)).toBe(false)
  })

  it('hides a section the operator switched off', () => {
    stubSettings({ [PortalSettingKey.SectionFaq]: SETTING_OFF })

    expect(useHomeSections().isVisible(HomeSection.Faq)).toBe(false)
  })

  it('shows projects once the operator switched them on', () => {
    stubSettings({ [PortalSettingKey.SectionProjects]: SETTING_ON })

    expect(useHomeSections().isVisible(HomeSection.Projects)).toBe(true)
  })
})
