/**
 * Mappers from the plugin DTOs to the card model: the one place that knows both the installed list's
 * and the registry's shape, so `PluginCard` knows neither.
 */
import { translate } from '@/lib/translate'
import { PluginCardBadge, PluginStatus } from '@/types/plugin'
import type { PluginCardModel, PluginListItemDto, PluginRegistryItemDto } from '@/types/plugin'

/**
 * Builds the card of an installed plugin.
 *
 * @param plugin - The installed plugin.
 * @param updateVersion - The newer version the registry offers for it, null when it is up to date.
 * @returns The card model; a refused plugin wears only the Refused badge and carries its broken rule.
 */
export const fromInstalled = (plugin: PluginListItemDto, updateVersion: string | null): PluginCardModel => {
  const refused = plugin.status === PluginStatus.Refused
  const badges: PluginCardBadge[] = []
  if (refused) {
    badges.push(PluginCardBadge.Refused)
  } else {
    badges.push(plugin.isLoaded ? PluginCardBadge.Active : PluginCardBadge.PendingRestart)
    if (plugin.stagedVersion !== null) {
      badges.push(PluginCardBadge.Staged)
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
    category: plugin.category,
    pricingLabel: '',
    description: plugin.description,
    tags: [],
    downloads: null,
    version: plugin.version,
    badges,
    updateVersion: refused ? null : updateVersion,
    stagedVersion: plugin.stagedVersion,
    refusal: refused && plugin.refusedRule ? { rule: plugin.refusedRule, detail: plugin.refusedDetail } : null,
  }
}

/**
 * Builds the card of a plugin the registry lists.
 *
 * @param item - The registry listing.
 * @param stagedVersion - Version of this plugin staged for the next restart, null when none.
 * @returns The card model; an installed plugin wears Active, a newer registry version adds Update.
 */
export const fromRegistry = (item: PluginRegistryItemDto, stagedVersion: string | null): PluginCardModel => {
  const badges: PluginCardBadge[] = []
  if (item.installed) {
    badges.push(PluginCardBadge.Active)
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
    category: translate(`plugins.pluginRegistry.category.${item.category}`),
    pricingLabel: translate(`plugins.pluginRegistry.pricing.${item.pricing}`),
    description: item.description,
    tags: item.tags ?? [],
    downloads: item.downloads ?? null,
    version: item.installedVersion ?? item.latestVersion,
    badges,
    updateVersion: item.updateAvailable ? item.latestVersion : null,
    stagedVersion,
    refusal: null,
  }
}
