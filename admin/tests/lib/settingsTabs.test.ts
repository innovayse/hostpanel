/**
 * Tests for the settings tab catalogue: every tab is listed once, in order, with its label key and cards.
 */
import { describe, expect, it } from 'vitest'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import { DEFAULT_SETTINGS_TAB, SETTINGS_TABS, SettingsTab } from '@/lib/settingsTabs'

/**
 * Finds the cards of one tab.
 *
 * @param tab - The tab to look up.
 * @returns Its cards, or an empty list when the tab is not catalogued.
 */
const cardsOf = (tab: SettingsTab) => SETTINGS_TABS.find((entry) => entry.tab === tab)?.cards ?? []

describe('SETTINGS_TABS', () => {
  it('lists every tab once, in display order', () => {
    expect(SETTINGS_TABS.map((entry) => entry.tab)).toEqual([
      SettingsTab.General,
      SettingsTab.Appearance,
      SettingsTab.Integrations,
      SettingsTab.Advanced,
    ])
  })

  it('names each label after its tab', () => {
    SETTINGS_TABS.forEach((entry) => {
      expect(entry.label).toBe(`settings.tabs.${entry.tab}`)
    })
  })

  it('places the cards on their tabs', () => {
    expect(cardsOf(SettingsTab.General)).toHaveLength(3)
    expect(cardsOf(SettingsTab.General)[0]).toBe(SiteIdentityCard)
    expect(cardsOf(SettingsTab.Appearance)).toHaveLength(5)
    expect(cardsOf(SettingsTab.Appearance)).toContain(BrandingCard)
    expect(cardsOf(SettingsTab.Integrations)).toEqual([TrackingChatCard])
  })

  it('leaves the advanced tab without cards, for the raw table', () => {
    expect(cardsOf(SettingsTab.Advanced)).toEqual([])
  })

  it('opens on the general tab', () => {
    expect(DEFAULT_SETTINGS_TAB).toBe(SettingsTab.General)
  })
})
