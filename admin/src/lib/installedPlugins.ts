/**
 * Narrowing of the installed plugin list for its search box and filters: the state a plugin is in,
 * and the pure filter the Installed tab applies to the store's list.
 */
import { PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'

/** Closed set of the states the Installed tab filters by; the strings are the values kept in the address. */
export const InstalledPluginState = {
  /** Loaded and running. */
  Active: 'active',
  /** On disk but waiting for a restart to load. */
  PendingRestart: 'pendingRestart',
  /** Skipped by the host because it broke a manifest rule. */
  Refused: 'refused',
} as const

/** One of the {@link InstalledPluginState} values. */
export type InstalledPluginState = (typeof InstalledPluginState)[keyof typeof InstalledPluginState]

/** What the Installed tab narrows by; an empty string means the criterion is not set. */
export interface InstalledPluginFilter {
  /** Text matched against the name, id and description, ignoring case. */
  search: string
  /** One of the {@link InstalledPluginState} values, empty for any state. */
  state: string
  /** The plugin category, empty for any category. */
  category: string
}

/**
 * The state a plugin is in on this server.
 *
 * @param plugin - The installed plugin.
 * @returns Refused for a plugin the host skipped, Active for a loaded one, PendingRestart otherwise.
 */
export const installedPluginState = (plugin: PluginListItemDto): InstalledPluginState => {
  if (plugin.status === PluginStatus.Refused) {
    return InstalledPluginState.Refused
  }
  return plugin.isLoaded ? InstalledPluginState.Active : InstalledPluginState.PendingRestart
}

/**
 * Narrows the installed plugins to those matching every criterion that is set.
 *
 * @param plugins - The installed plugins.
 * @param filter - The search text, state and category; an empty criterion matches everything.
 * @returns The matching plugins in their original order.
 */
export const filterInstalledPlugins = (plugins: PluginListItemDto[], filter: InstalledPluginFilter): PluginListItemDto[] => {
  const needle = filter.search.trim().toLowerCase()
  return plugins.filter(plugin =>
    (needle === '' || [plugin.name, plugin.id, plugin.description].some(text => text.toLowerCase().includes(needle)))
    && (filter.state === '' || installedPluginState(plugin) === filter.state)
    && (filter.category === '' || plugin.category === filter.category),
  )
}
