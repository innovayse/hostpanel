/**
 * Mappers from the plugin DTOs to the card model: the one place that knows both the installed list's
 * and the registry's shape, so `PluginCard` knows neither.
 */
import { enumValueOf } from '@/lib/enumValue'
import { InstalledPluginState, installedPluginState } from '@/lib/installedPlugins'
import { translate } from '@/lib/translate'
import { PluginCardBadge, PluginCategory } from '@/types/plugin'
import type { PluginCardFailure, PluginCardModel, PluginListItemDto, PluginRegistryItemDto } from '@/types/plugin'
import { failedChangeOf } from '@/lib/pluginChange'

/**
 * The words an operator reads for a plugin's category, the same for an installed plugin and a registry listing.
 * A registry category is one of {@link PluginCategory} and is translated; an installed plugin declares its own
 * display label in its manifest (such as "Hosting / Provisioning"), which is shown as written unless it names
 * one of the registry categories.
 *
 * @param category - A registry category name or a manifest's display label.
 * @returns The translated registry category, or the label itself when it is not a registry category.
 */
export const categoryLabel = (category: string): string => {
  const known = enumValueOf(PluginCategory, category)
  return known === null ? category : translate(`plugins.pluginRegistry.category.${known}`)
}

/** The badge each state of a plugin that was neither refused nor failed wears; the card reads the same state as the filter and the details page. */
const CARD_BADGE_OF_STATE: Readonly<Record<InstalledPluginState, PluginCardBadge>> = {
  [InstalledPluginState.Active]: PluginCardBadge.Active,
  [InstalledPluginState.Disabled]: PluginCardBadge.Disabled,
  [InstalledPluginState.PendingRestart]: PluginCardBadge.PendingRestart,
  [InstalledPluginState.Refused]: PluginCardBadge.Refused,
  [InstalledPluginState.LoadFailed]: PluginCardBadge.LoadFailed,
}

/**
 * Builds the card of an installed plugin.
 *
 * @param plugin - The installed plugin.
 * @param updateVersion - The newer version the registry offers for it, null when it is up to date.
 * @returns The card model; a refused plugin wears only the Refused badge and carries its broken rule, and a plugin
 * whose assembly failed to load wears only the LoadFailed badge and carries the load error as the rule's detail.
 */
export const fromInstalled = (plugin: PluginListItemDto, updateVersion: string | null): PluginCardModel => {
  const state = installedPluginState(plugin)
  const refused = state === InstalledPluginState.LoadFailed || state === InstalledPluginState.Refused
  const badges: PluginCardBadge[] = []
  const failure: PluginCardFailure | null = failedChangeOf(plugin)
  if (state === InstalledPluginState.LoadFailed) {
    badges.push(PluginCardBadge.LoadFailed)
  } else if (state === InstalledPluginState.Refused) {
    badges.push(PluginCardBadge.Refused)
  } else {
    badges.push(CARD_BADGE_OF_STATE[state])
    if (plugin.stagedVersion !== null) {
      badges.push(PluginCardBadge.Staged)
    }
    if (failure !== null) {
      badges.push(PluginCardBadge.ChangeFailed)
    }
    if (updateVersion) {
      badges.push(PluginCardBadge.Update)
    }
  }
  if (plugin.isBuiltIn) {
    badges.push(PluginCardBadge.BuiltIn)
  }
  return {
    id: plugin.id,
    name: plugin.name,
    iconUrl: plugin.iconUrl,
    color: plugin.color,
    verified: false,
    publisher: plugin.author,
    category: categoryLabel(plugin.category),
    pricingLabel: '',
    description: plugin.description,
    tags: [],
    downloads: null,
    version: plugin.version,
    badges,
    updateVersion: refused ? null : updateVersion,
    stagedVersion: plugin.stagedVersion,
    refusal: refused && plugin.refusedRule ? { rule: plugin.refusedRule, detail: plugin.refusedDetail } : null,
    failedChange: failure,
  }
}

/**
 * Builds the card of a plugin the registry lists.
 *
 * @param item - The registry listing.
 * @param installedPlugin - This plugin's row in the installed list, null when it is not installed or the list is not read yet.
 * @returns The card model; an installed plugin wears the badge of the state the installed list gives it (the same
 * mapping as {@link fromInstalled}, so the two cards always agree), a staged upload adds Staged and a newer registry
 * version adds Update.
 */
export const fromRegistry = (item: PluginRegistryItemDto, installedPlugin: PluginListItemDto | null): PluginCardModel => {
  const stagedVersion = installedPlugin?.stagedVersion ?? null
  const badges: PluginCardBadge[] = []
  if (installedPlugin !== null) {
    badges.push(CARD_BADGE_OF_STATE[installedPluginState(installedPlugin)])
  }
  if (stagedVersion !== null) {
    badges.push(PluginCardBadge.Staged)
  }
  if (item.updateAvailable) {
    badges.push(PluginCardBadge.Update)
  }
  return {
    id: item.id,
    name: item.name,
    iconUrl: item.iconUrl,
    color: null,
    verified: item.verified,
    publisher: item.author,
    category: categoryLabel(item.category),
    pricingLabel: translate(`plugins.pluginRegistry.pricing.${item.pricing}`),
    description: item.description,
    tags: item.tags ?? [],
    downloads: item.downloads ?? null,
    version: item.installedVersion ?? item.latestVersion,
    badges,
    updateVersion: item.updateAvailable ? item.latestVersion : null,
    stagedVersion,
    refusal: null,
    failedChange: null,
  }
}
