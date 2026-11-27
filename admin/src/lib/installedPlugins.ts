/**
 * Narrowing of the installed plugin list for its search box and filters: the state a plugin is in, the words and tone
 * every screen shows it with, and the pure filter the Installed tab applies to the store's list.
 */
import { PluginManifestRule, PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { UiTone } from '@/utils/ui/tone'

/** Closed set of the states the Installed tab filters by; the strings are the values kept in the address. */
export const InstalledPluginState = {
  /** Loaded and switched on. */
  Active: 'active',
  /** Loaded, but its enabled switch is off, so it does nothing. */
  Disabled: 'disabled',
  /** On disk but waiting for a restart to load. */
  PendingRestart: 'pendingRestart',
  /** Skipped by the host because it broke a manifest rule. */
  Refused: 'refused',
  /** Installed, but its assembly failed to load, so it does not run. */
  LoadFailed: 'loadFailed',
} as const

/** One of the {@link InstalledPluginState} values. */
export type InstalledPluginState = (typeof InstalledPluginState)[keyof typeof InstalledPluginState]

/** How a state looks wherever it is shown: the locale key of its name and the tone of its badge. */
export interface InstalledPluginStateLook {
  /** Locale key of the state's name. */
  labelKey: string
  /** Tone of the state's badge. */
  tone: UiTone
}

/**
 * The one table from a state to its name and tone, read by the card badge, the state filter and the details page, so
 * a plugin in one state is worded and coloured the same on every screen.
 */
export const INSTALLED_PLUGIN_STATE_LOOK: Readonly<Record<InstalledPluginState, InstalledPluginStateLook>> = {
  [InstalledPluginState.Active]: { labelKey: 'shared.active', tone: UiTone.Success },
  [InstalledPluginState.Disabled]: { labelKey: 'shared.disabled', tone: UiTone.Neutral },
  [InstalledPluginState.PendingRestart]: { labelKey: 'plugins.pluginCard.pendingRestart', tone: UiTone.Warning },
  [InstalledPluginState.Refused]: { labelKey: 'plugins.pluginCard.refused', tone: UiTone.Danger },
  [InstalledPluginState.LoadFailed]: { labelKey: 'plugins.pluginCard.loadFailed', tone: UiTone.Danger },
}

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
 * Whether the plugin is installed but its assembly failed to load; the server names that failure as a rule of its own,
 * whose detail is the load error.
 *
 * @param plugin - The installed plugin.
 * @returns True when the plugin's rule is {@link PluginManifestRule.AssemblyLoadFailed}.
 */
const isLoadFailed = (plugin: PluginListItemDto): boolean => plugin.refusedRule === PluginManifestRule.AssemblyLoadFailed

/**
 * The state a plugin is in on this server.
 *
 * @param plugin - The installed plugin.
 * @returns LoadFailed for a plugin whose assembly did not load, Refused for one the host skipped, Active for a loaded
 * one that is switched on, Disabled for a loaded one that is switched off, PendingRestart otherwise.
 */
export const installedPluginState = (plugin: PluginListItemDto): InstalledPluginState => {
  if (isLoadFailed(plugin)) {
    return InstalledPluginState.LoadFailed
  }
  if (plugin.status === PluginStatus.Refused) {
    return InstalledPluginState.Refused
  }
  if (!plugin.isLoaded) {
    return InstalledPluginState.PendingRestart
  }
  return plugin.isEnabled ? InstalledPluginState.Active : InstalledPluginState.Disabled
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
