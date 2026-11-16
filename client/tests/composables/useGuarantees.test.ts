/**
 * Tests for `useGuarantees`: each promise shows only when set, the money-back one with its days.
 *
 * @module tests/composables/useGuarantees.test
 */

import { describe, expect, it } from 'vitest'
import { GuaranteeKey } from '~/constants/guaranteeKey'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { useGuarantees } from '~/composables/useGuarantees'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useGuarantees', () => {
  it('is empty when nothing is set', async () => {
    await loadPortalSettings(replies)

    expect(useGuarantees().value).toEqual([])
  })

  it('returns every promise in enum order, money back with its days', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.GuaranteeNoHiddenFees]: SETTING_ON,
      [PortalSettingKey.GuaranteeMoneyBackDays]: '30',
      [PortalSettingKey.MigrationFree]: SETTING_ON,
    })

    expect(useGuarantees().value).toEqual([
      { key: GuaranteeKey.FreeMigration },
      { key: GuaranteeKey.MoneyBack, days: 30 },
      { key: GuaranteeKey.NoHiddenFees },
    ])
  })

  it('ignores a money-back period that is zero or not digits', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.GuaranteeMoneyBackDays]: '0' })
    expect(useGuarantees().value).toEqual([])

    await loadPortalSettings(replies, { [PortalSettingKey.GuaranteeMoneyBackDays]: 'thirty' })
    expect(useGuarantees().value).toEqual([])
  })
})
