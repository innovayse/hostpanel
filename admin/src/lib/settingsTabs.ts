/**
 * The cards each tab of the system settings screen shows.
 *
 * The tabs themselves (their order, keys and captions) are listed once, in the screen's schema
 * (`views/SystemSettingsView.schema.ts`), which the skeleton draws too. This catalogue is the one place that says which
 * card sits on which tab, so moving a card is one edit here.
 */
import type { Component } from 'vue'
import BrandColorsCard from '@/components/settings/BrandColorsCard.vue'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import HomeSectionsCard from '@/components/settings/HomeSectionsCard.vue'
import HostingDetailsCard from '@/components/settings/HostingDetailsCard.vue'
import LegalTextsCard from '@/components/settings/LegalTextsCard.vue'
import PortalAppearanceCard from '@/components/settings/PortalAppearanceCard.vue'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import StorefrontContentCard from '@/components/settings/StorefrontContentCard.vue'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import TypographyCard from '@/components/settings/TypographyCard.vue'
import { SettingsTab } from '@/types/setting'

/** The tab the screen opens on when the address names none. */
export const DEFAULT_SETTINGS_TAB: SettingsTab = SettingsTab.General

/** The cards of each tab that has cards, in display order, by the tab's address value. */
const TAB_CARDS: ReadonlyMap<string, readonly Component[]> = new Map<string, readonly Component[]>([
  [SettingsTab.General, [SiteIdentityCard, StorefrontContentCard, LegalTextsCard, HostingDetailsCard]],
  [SettingsTab.Appearance, [PortalAppearanceCard, HomeSectionsCard, BrandColorsCard, TypographyCard, BrandingCard]],
  [SettingsTab.Integrations, [TrackingChatCard]],
])

/**
 * The cards a tab shows.
 *
 * @param tab - The tab's address value, as the screen's schema keys it.
 * @returns Its cards in order; empty for a tab the screen renders itself (the Advanced tab's raw table).
 */
export const cardsOfTab = (tab: string): readonly Component[] => TAB_CARDS.get(tab) ?? []
