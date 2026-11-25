/**
 * Reads the last change the host made to a plugin (`lastChange` of a list item): whether it failed, and the sentence
 * that explains the failure. One implementation for the plugin card, the details header and the restart dialog.
 */
import { translate } from '@/lib/translate'
import { PluginChangeResult } from '@/types/plugin'
import type { PluginCardFailure, PluginListItemDto } from '@/types/plugin'

/**
 * Reads the failure of a plugin's last change.
 *
 * @param plugin - The listed plugin.
 * @returns The reason and the restored version when the last change failed, null otherwise.
 */
export const failedChangeOf = (plugin: PluginListItemDto): PluginCardFailure | null => {
  const change = plugin.lastChange
  if (change === undefined || change === null || change.result !== PluginChangeResult.Failed) {
    return null
  }
  return { error: change.error, restoredVersion: change.restoredVersion }
}

/**
 * The sentence for a failed change: the reason, and the version the host kept running when it restored one.
 *
 * @param failure - The failed change.
 * @returns The sentence in the operator's language.
 */
export const failureSentence = (failure: PluginCardFailure): string => {
  const reason = failure.error ?? translate('plugins.pluginCard.changeFailedUnknown')
  return failure.restoredVersion === null
    ? translate('plugins.pluginCard.changeFailedReason', { error: reason })
    : translate('plugins.pluginCard.changeFailedRestored', { error: reason, version: failure.restoredVersion })
}
