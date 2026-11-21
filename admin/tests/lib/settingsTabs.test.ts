/**
 * Tests for the settings tab catalogue: each tab of the screen's schema has its cards, the Advanced tab has none, and
 * the schema is the one list of the tabs.
 */
import { describe, expect, it } from 'vitest'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import LegalTextsCard from '@/components/settings/LegalTextsCard.vue'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import { DEFAULT_SETTINGS_TAB, cardsOfTab } from '@/lib/settingsTabs'
import { SettingsTab } from '@/types/setting'
import schema from '@/views/SystemSettingsView.schema'

describe('cardsOfTab', () => {
  it('places the cards on their tabs', () => {
    expect(cardsOfTab(SettingsTab.General)).toHaveLength(4)
    expect(cardsOfTab(SettingsTab.General)[0]).toBe(SiteIdentityCard)
    expect(cardsOfTab(SettingsTab.General)).toContain(LegalTextsCard)
    expect(cardsOfTab(SettingsTab.Appearance)).toHaveLength(5)
    expect(cardsOfTab(SettingsTab.Appearance)).toContain(BrandingCard)
    expect(cardsOfTab(SettingsTab.Integrations)).toEqual([TrackingChatCard])
  })

  it('leaves the advanced tab without cards, for the raw table', () => {
    expect(cardsOfTab(SettingsTab.Advanced)).toEqual([])
  })
})

describe('the settings tabs of the screen schema', () => {
  it('lists every tab once, in display order', () => {
    expect(schema.tabs.map((entry) => entry.key)).toEqual([
      SettingsTab.General,
      SettingsTab.Appearance,
      SettingsTab.Integrations,
      SettingsTab.Advanced,
    ])
  })

  it('names each label after its tab', () => {
    schema.tabs.forEach((entry) => {
      expect(entry.label).toBe(`settings.tabs.${entry.key}`)
    })
  })

  it('opens on the general tab', () => {
    expect(DEFAULT_SETTINGS_TAB).toBe(SettingsTab.General)
  })
})
