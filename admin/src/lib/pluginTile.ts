/**
 * Badges the catalogue grid and the installed grid word the same way on a plugin tile, kept in one
 * place so the two never drift apart.
 */
import type { ComposerTranslation } from 'vue-i18n'
import { PluginTileBadgeKey } from '@/types/pluginTile'
import type { PluginTileBadge } from '@/types/pluginTile'
import { UiTone } from '@/utils/ui/tone'

/**
 * Badge for a newer upload that waits for the restart.
 *
 * @param t - Translation function.
 * @param version - The staged version.
 * @returns The staged badge.
 */
export const stagedBadge = (t: ComposerTranslation, version: string): PluginTileBadge => ({
  key: PluginTileBadgeKey.Staged,
  label: t('plugins.pluginCard.staged', { version }),
  tone: UiTone.Info,
})

/**
 * Badge for a newer version the registry offers.
 *
 * @param t - Translation function.
 * @param version - The offered version.
 * @returns The update badge.
 */
export const updateBadge = (t: ComposerTranslation, version: string): PluginTileBadge => ({
  key: PluginTileBadgeKey.Update,
  label: t('plugins.pluginCard.updateAvailable', { version }),
  tone: UiTone.Warning,
})
