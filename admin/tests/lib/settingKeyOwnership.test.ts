/**
 * Tests for the settings key ownership: the card-owned set is exactly the three key enums.
 */
import { describe, expect, it } from 'vitest'
import { CARD_OWNED_KEYS } from '@/lib/settingKeyOwnership'
import { BillingSettingKey, PluginSettingKey, SettingKey } from '@/types/setting'

describe('CARD_OWNED_KEYS', () => {
  it('holds every storefront, plugin and billing card key', () => {
    for (const key of [...Object.values(SettingKey), ...Object.values(PluginSettingKey), ...Object.values(BillingSettingKey)]) {
      expect(CARD_OWNED_KEYS.has(key)).toBe(true)
    }
    expect(CARD_OWNED_KEYS.size).toBe(
      Object.values(SettingKey).length + Object.values(PluginSettingKey).length + Object.values(BillingSettingKey).length,
    )
  })

  it('leaves out a key no card edits', () => {
    expect(CARD_OWNED_KEYS.has('billing.tax.enabled')).toBe(false)
  })
})
