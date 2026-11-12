<script setup lang="ts">
/**
 * The installed plugins as a grid of {@link PluginTile}s (three columns on a wide screen, two on a
 * medium one, one on a narrow one), with placeholder tiles while the list loads. The empty state is
 * left to the screen.
 *
 * Each plugin is mapped into the shared tile: the author and category on the meta line, the active
 * or pending-restart badge, the staged, update, built-in and capability badges, and its version in
 * the footer. A settings button opens the plugin's own page, and so does the name. A newer registry
 * version adds an Update button; a plugin shipped with the server build has no Remove button, since
 * the server refuses it. A refused plugin (one that broke a manifest rule) shows a Refused badge in
 * place of a loaded status, with its id, the broken rule and the localised reason; it is never
 * shown as enabled, has no page to open, and can only be removed.
 */
import { useI18n } from 'vue-i18n'
import PluginTile from '@/components/plugins/PluginTile.vue'
import PluginTileSkeleton from '@/components/plugins/PluginTileSkeleton.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { stagedBadge, updateBadge } from '@/lib/pluginTile'
import { PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { PluginTileBadgeKey } from '@/types/pluginTile'
import type { PluginTileBadge } from '@/types/pluginTile'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginInstalledGrid. */
const props = withDefaults(defineProps<{
  /** The installed plugins to show. */
  plugins: PluginListItemDto[]
  /** True while the list loads: placeholder tiles are drawn in place of the plugins. */
  loading?: boolean
  /** Id of the plugin whose removal is in flight, null when none. */
  removingId?: string | null
  /** The newer version the registry offers, per plugin id; a plugin without an entry is up to date. */
  updateVersions?: Record<string, string>
  /** Ids of the plugins whose update from the registry is in flight. */
  updatingIds?: string[]
  /** How many placeholder tiles to draw while loading. */
  skeletonCount?: number
}>(), {
  loading: false,
  removingId: null,
  /**
   * Default for `updateVersions`.
   *
   * @returns No update on offer.
   */
  updateVersions: () => ({}),
  /**
   * Default for `updatingIds`.
   *
   * @returns No update in flight.
   */
  updatingIds: () => [],
  skeletonCount: 3,
})

/** Events emitted by PluginInstalledGrid. */
const emit = defineEmits<{
  /** Emitted with the plugin id when the admin asks to remove it. */
  remove: [id: string]
  /** Emitted with the plugin id and the newer version when the admin asks to update it from the registry. */
  update: [id: string, version: string]
  /** Emitted with the plugin id when the admin opens the plugin's own page. */
  settings: [id: string]
}>()

/**
 * Whether the host skipped the plugin because it broke a manifest rule.
 *
 * @param plugin - The installed plugin.
 * @returns True for a refused plugin.
 */
const isRefused = (plugin: PluginListItemDto): boolean => plugin.status === PluginStatus.Refused

/**
 * The localised reason for the rule a refused plugin broke.
 *
 * @param plugin - The installed plugin.
 * @returns The sentence; empty when the plugin is not refused.
 */
const reasonOf = (plugin: PluginListItemDto): string =>
  plugin.refusedRule === null ? '' : t(`plugins.rules.${plugin.refusedRule}`)

/**
 * The badges of one tile: the refused or running state, then staged, update, built-in and one per
 * declared capability.
 *
 * @param plugin - The installed plugin.
 * @returns The badges in display order.
 */
const badgesOf = (plugin: PluginListItemDto): PluginTileBadge[] => {
  const badges: PluginTileBadge[] = []
  if (isRefused(plugin)) {
    badges.push({ key: PluginTileBadgeKey.Refused, label: t('plugins.pluginCard.refused'), tone: UiTone.Danger })
  } else {
    badges.push({
      key: PluginTileBadgeKey.Status,
      label: plugin.isLoaded ? t('shared.active') : t('plugins.pluginCard.pendingRestart'),
      tone: plugin.isLoaded ? UiTone.Success : UiTone.Warning,
    })
  }
  if (plugin.stagedVersion !== null) {
    badges.push(stagedBadge(t, plugin.stagedVersion))
  }
  const update = props.updateVersions[plugin.id]
  if (update) {
    badges.push(updateBadge(t, update))
  }
  if (plugin.isBuiltIn) {
    badges.push({ key: PluginTileBadgeKey.BuiltIn, label: t('plugins.pluginCard.builtIn'), tone: UiTone.Info })
  }
  for (const capability of plugin.capabilities) {
    badges.push({
      key: `capability-${capability}`,
      label: t(`plugins.pluginCard.capabilities.${capability}`),
      tone: UiTone.Neutral,
    })
  }
  return badges
}

/**
 * Whether the plugin's own update or removal is running; its buttons are disabled then.
 *
 * @param id - The plugin id.
 * @returns True while this plugin is busy.
 */
const isBusy = (id: string): boolean => props.updatingIds.includes(id) || props.removingId === id
</script>

<template>
  <UiGrid :columns="3" gap="md" :aria-busy="loading" data-testid="plugin-installed-grid">
    <template v-if="loading">
      <PluginTileSkeleton v-for="n in skeletonCount" :key="n" />
    </template>
    <template v-else>
      <PluginTile
        v-for="plugin in plugins"
        :key="plugin.id"
        :id="plugin.id"
        :name="plugin.name"
        :icon-url="plugin.iconUrl"
        :color="plugin.color"
        :openable="!isRefused(plugin)"
        :meta="[plugin.author, plugin.category]"
        :description="plugin.description"
        :badges="badgesOf(plugin)"
        :version="plugin.version"
        :data-testid="`plugin-card-${plugin.id}`"
        @open="emit('settings', $event)"
      >
        <UiStack v-if="isRefused(plugin)" gap="xs" :data-testid="`plugin-refusal-${plugin.id}`">
          <UiText tone="danger" size="sm">{{ reasonOf(plugin) }}</UiText>
          <UiText tone="muted" size="xs" class="font-mono">
            {{ t('plugins.pluginCard.brokenRule', { id: plugin.id, rule: plugin.refusedRule }) }}
          </UiText>
          <UiText v-if="plugin.refusedDetail" tone="muted" size="xs" class="font-mono break-all">
            {{ plugin.refusedDetail }}
          </UiText>
        </UiStack>
        <template #actions>
          <UiButton
            v-if="updateVersions[plugin.id]"
            type="button"
            :variant="UiButtonVariant.Primary"
            size="sm"
            :disabled="isBusy(plugin.id)"
            :data-testid="`plugin-update-button-${plugin.id}`"
            @click="emit('update', plugin.id, updateVersions[plugin.id])"
          >
            {{ updatingIds.includes(plugin.id) ? t('plugins.pluginRegistry.installing') : t('plugins.pluginRegistry.update', { version: updateVersions[plugin.id] }) }}
          </UiButton>
          <UiIconButton
            v-if="!isRefused(plugin)"
            :label="t('plugins.pluginTile.settings')"
            size="sm"
            :data-testid="`plugin-settings-${plugin.id}`"
            @click="emit('settings', plugin.id)"
          >
            <UiIcon :markup="Icon.Settings" size="sm" />
          </UiIconButton>
          <UiButton
            v-if="!plugin.isBuiltIn"
            type="button"
            :variant="UiButtonVariant.Danger"
            size="sm"
            :disabled="isBusy(plugin.id)"
            :data-testid="`plugin-remove-${plugin.id}`"
            @click="emit('remove', plugin.id)"
          >
            {{ removingId === plugin.id ? t('plugins.pluginCard.removing') : t('plugins.pluginCard.remove') }}
          </UiButton>
        </template>
      </PluginTile>
    </template>
  </UiGrid>
</template>
