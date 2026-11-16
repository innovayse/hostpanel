/**
 * Tests for `useHomeSections`: an explicit switch wins, a missing one falls back to the
 * section default.
 *
 * @module tests/composables/useHomeSections.test
 */

import { describe, expect, it } from 'vitest'
import { HomeSection } from '~/constants/homeSection'
import { SETTING_OFF, SETTING_ON } from '~/constants/settingValue'
import { useHomeSections } from '~/composables/useHomeSections'
import { homeSectionSettings, loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useHomeSections', () => {
  it('shows every section but projects when nothing is set', async () => {
    await loadPortalSettings(replies)
    const { isVisible } = useHomeSections()

    expect(isVisible(HomeSection.Faq)).toBe(true)
    expect(isVisible(HomeSection.Locations)).toBe(true)
    expect(isVisible(HomeSection.Projects)).toBe(false)
  })

  it('hides a section the operator switched off', async () => {
    await loadPortalSettings(replies, homeSectionSettings([HomeSection.Faq], SETTING_OFF))

    expect(useHomeSections().isVisible(HomeSection.Faq)).toBe(false)
  })

  it('shows projects once the operator switched them on', async () => {
    await loadPortalSettings(replies, homeSectionSettings([HomeSection.Projects], SETTING_ON))

    expect(useHomeSections().isVisible(HomeSection.Projects)).toBe(true)
  })
})
