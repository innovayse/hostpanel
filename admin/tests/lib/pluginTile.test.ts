/**
 * Tests for the plugin tile badge builders: the staged and the update badge word, key and colour.
 */
import { describe, expect, it } from 'vitest'
import type { ComposerTranslation } from 'vue-i18n'
import { stagedBadge, updateBadge } from '@/lib/pluginTile'
import { PluginTileBadgeKey } from '@/types/pluginTile'
import { UiTone } from '@/utils/ui/tone'

/**
 * Stand-in translation that echoes the key with its version, so a test reads the key it asked for.
 *
 * @param key - Message key.
 * @param params - Message parameters.
 * @returns The key and the version.
 */
const t = ((key: string, params?: { version?: string }) => `${key}:${params?.version ?? ''}`) as unknown as ComposerTranslation

describe('stagedBadge', () => {
  it('words the staged version in the info tone under the staged key', () => {
    expect(stagedBadge(t, '1.3.0')).toEqual({
      key: PluginTileBadgeKey.Staged,
      label: 'plugins.pluginCard.staged:1.3.0',
      tone: UiTone.Info,
    })
  })
})

describe('updateBadge', () => {
  it('words the offered version in the warning tone under the update key', () => {
    expect(updateBadge(t, '2.0.0')).toEqual({
      key: PluginTileBadgeKey.Update,
      label: 'plugins.pluginCard.updateAvailable:2.0.0',
      tone: UiTone.Warning,
    })
  })
})
