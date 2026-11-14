/**
 * Mappers from the plugin DTOs to the card model: the one place that knows both the installed list's
 * and the registry's shape, so `PluginCard` knows neither.
 */
import { enumValueOf } from '@/lib/enumValue'
import { translate } from '@/lib/translate'
import { PluginCardBadge, PluginCategory, PluginStatus } from '@/types/plugin'
import type { PluginCardModel, PluginListItemDto, PluginRegistryItemDto } from '@/types/plugin'

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
  }
}
