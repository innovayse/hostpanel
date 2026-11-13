/**
 * The navigation sections the panel offers right now: the one list the sidebar draws and the command
 * palette searches, so the two can never disagree about which pages exist.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { NAV_SECTIONS, withAddons } from '@/lib/navigation'
import type { NavSection } from '@/lib/navigation'
import { usePluginUiStore } from '@/stores/pluginUi'

/**
 * Builds the live list of navigation sections.
 *
 * The panel's own sections come first in their configured order, with the Add-ons section while a plugin
 * declares a menu item; a section with no item left is dropped, so no empty label is drawn.
 *
 * @returns A computed list that follows the plugin menu as it loads.
 */
export const useNavSections = (): ComputedRef<NavSection[]> => {
  /** Plugin ui store: the menu items plugins declare. */
  const pluginUi = usePluginUiStore()

  return computed<NavSection[]>(() => withAddons(NAV_SECTIONS, pluginUi.menu).filter((section) => section.items.length > 0))
}
