/**
 * Tests for `useContactHours`: the operator's working hours and time zone, hidden when unset.
 *
 * @module tests/composables/useContactHours.test
 */

import { describe, expect, it } from 'vitest'
import { useContactHours } from '~/composables/useContactHours'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useContactHours', () => {
  it('joins the hours and the time zone the operator set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactHours]: '08:00 - 17:00', [PortalSettingKey.ContactTimezone]: 'UTC+2' })

    expect(useContactHours().workingHours.value).toBe('08:00 - 17:00 UTC+2')
  })

  it('shows an IANA time zone as its current offset', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactHours]: '08:00 - 17:00', [PortalSettingKey.ContactTimezone]: 'Asia/Yerevan' })

    expect(useContactHours().workingHours.value).toBe('08:00 - 17:00 UTC+04:00')
  })

  it('shows the hours alone when no time zone is set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactHours]: '08:00 - 17:00' })

    expect(useContactHours().workingHours.value).toBe('08:00 - 17:00')
  })

  it('is empty when no hours are set, even with a time zone', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactTimezone]: 'UTC+2' })

    expect(useContactHours().workingHours.value).toBe('')
  })

  it('uses the hours written for the visitor language, with the one time zone', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.ContactHours]: 'Mon-Fri',
      [PortalSettingKey.ContactHoursRu]: 'Пн-Пт',
      [PortalSettingKey.ContactTimezone]: 'UTC+4',
    })
    stubLocale('ru')

    expect(useContactHours().workingHours.value).toBe('Пн-Пт UTC+4')
  })

  it('falls back to the default hours when the language has none', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactHours]: 'Mon-Fri' })
    stubLocale('hy')

    expect(useContactHours().workingHours.value).toBe('Mon-Fri')
  })
})
