/**
 * The tabs of the system settings screen and the cards each one shows.
 *
 * The catalogue is the one place that says which card sits on which tab; the screen renders from it, so
 * moving a card is one edit here.
 */
import type { Component } from 'vue'
import BrandColorsCard from '@/components/settings/BrandColorsCard.vue'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import HomeSectionsCard from '@/components/settings/HomeSectionsCard.vue'
import HostingDetailsCard from '@/components/settings/HostingDetailsCard.vue'
import PortalAppearanceCard from '@/components/settings/PortalAppearanceCard.vue'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import StorefrontContentCard from '@/components/settings/StorefrontContentCard.vue'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import TypographyCard from '@/components/settings/TypographyCard.vue'

/** Closed set of tabs on the system settings screen; the value is what the address holds in `?tab=`. */
export enum SettingsTab {
  /** The storefront's identity, content and hosting details. */
  General = 'general',
  /** The portal's look: appearance, home sections, colours, typography and branding. */
  Appearance = 'appearance',
  /** Third-party tracking and chat. */
  Integrations = 'integrations',
  /** The raw key-value list of every global setting. */
  Advanced = 'advanced',
}

/** One tab of the screen. */
export interface SettingsTabDefinition {
  /** Which tab this is. */
  tab: SettingsTab
  /** Locale key of the tab's label. */
  label: string
  /** The cards shown on the tab, in order; empty for a tab the screen renders itself. */
  cards: readonly Component[]
}

/** The tab the screen opens on when the address names none. */
export const DEFAULT_SETTINGS_TAB: SettingsTab = SettingsTab.General

/** Every tab in display order, with its label key and cards. The Advanced tab is the raw table, drawn by the view. */
export const SETTINGS_TABS: readonly SettingsTabDefinition[] = [
  {
    tab: SettingsTab.General,
    label: 'settings.tabs.general',
    cards: [SiteIdentityCard, StorefrontContentCard, HostingDetailsCard],
  },
  {
    tab: SettingsTab.Appearance,
    label: 'settings.tabs.appearance',
    cards: [PortalAppearanceCard, HomeSectionsCard, BrandColorsCard, TypographyCard, BrandingCard],
  },
  {
    tab: SettingsTab.Integrations,
    label: 'settings.tabs.integrations',
    cards: [TrackingChatCard],
  },
  {
    tab: SettingsTab.Advanced,
    label: 'settings.tabs.advanced',
    cards: [],
  },
]
