/**
 * Mount helper for the cards of the System Settings page: a card is mounted under a host that provides
 * the page's settings draft registry, exactly as `SystemSettingsView` does, so a test can edit a control
 * and then save it the way the sticky bar does.
 */
import { defineComponent, h } from 'vue'
import type { Component } from 'vue'
import type { VueWrapper } from '@vue/test-utils'
import { provideSettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { SettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { Setting } from '@/types/setting'
import { mountScreen } from './mount'

/** A mounted settings card with the registry its drafts registered in. */
export interface MountedSettingsCard {
  /** The mounted host; the card is inside it. */
  wrapper: VueWrapper
  /** The page registry: its `saveAll` is the sticky bar's Save. */
  registry: SettingsDraftRegistry
}

/**
 * Mounts a settings card under a host that provides the page registry.
 *
 * @param card - The card to mount.
 * @param settings - The settings rows handed to the card.
 * @param props - Further props of the card (a toggle field takes its key and label here).
 * @returns The wrapper and the registry.
 */
export const mountSettingsCard = async (card: Component, settings: Setting[], props: Record<string, unknown> = {}): Promise<MountedSettingsCard> => {
  /** Holds the registry the host creates while it sets up. */
  const holder: { registry: SettingsDraftRegistry | null } = { registry: null }
  const Host = defineComponent({
    setup: () => {
      holder.registry = provideSettingsDraftRegistry()
      return () => h(card, { ...props, settings })
    },
  })
  const { wrapper } = await mountScreen(Host)
  if (!holder.registry) {
    throw new Error('The host did not create a settings draft registry.')
  }
  return { wrapper, registry: holder.registry }
}
