<script setup lang="ts">
/**
 * Displays a single installed plugin as a glass card: a logo tile, the name with its version, the state
 * badge, the description, and the actions under a divider; it has a remove button; a plugin shipped with
 * the server build is marked built-in and has no remove button, since the server refuses it.
 *
 * A refused plugin (one that broke a manifest rule) shows a Refused badge instead of a loaded
 * status, with its id, the broken rule and the localised reason; it is never shown as enabled and
 * can only be removed. A newer upload waiting for the restart shows its staged version, a newer version in the
 * registry shows an update badge with an Update button, a plugin the registry lists has a Details button, and each capability the manifest declares (such as serving pages from a
 * fulfilment plugin) is shown as a badge.
 *
 * While the installed list loads the card is drawn with no plugin and `loading` on: the same header, body and footer,
 * with placeholder bars where the plugin's own text and badges go and the remove button disabled.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginCard. */
const props = withDefaults(defineProps<{
  /** The installed plugin to show; null while the installed list loads. */
  plugin: PluginListItemDto | null
  /** True while the installed list loads: the card draws placeholder bars in place of the plugin's data. */
  loading?: boolean
  /** True while this plugin's removal is in flight. */
  removing?: boolean
  /** The newer version the registry offers for this plugin; null or omitted when it is up to date. */
  updateVersion?: string | null
  /** Release notes of the newer registry version as Markdown; null when the registry sent none. */
  updateChangelog?: string | null
  /** True while this plugin's update from the registry is in flight. */
  updating?: boolean
  /** True when the registry lists this plugin, so it has a details page to open. */
  hasDetails?: boolean
}>(), {
  loading: false,
  removing: false,
  updateVersion: null,
  updateChangelog: null,
  updating: false,
  hasDetails: false,
})

/** True when the host skipped this plugin because it broke a manifest rule. */
const refused = computed<boolean>(() => props.plugin?.status === PluginStatus.Refused)

/** Id of the plugin; empty while the card is a placeholder. */
const pluginId = computed<string>(() => props.plugin?.id ?? '')

/** Localised reason for the broken rule; empty when the plugin is not refused. */
const reason = computed<string>(() =>
  props.plugin?.refusedRule ? t(`plugins.rules.${props.plugin.refusedRule}`) : '',
)

/** Whether the release notes of the installed version are open. */
const notesOpen = ref(false)

/** Whether the release notes of the offered update are open. */
const updateNotesOpen = ref(false)

/** Events emitted by PluginCard. */
const emit = defineEmits<{
  /** Emitted with the plugin id when the admin asks to remove it. */
  remove: [id: string]
  /** Emitted with the plugin id and the newer version when the admin asks to update it from the registry. */
  update: [id: string, version: string]
  /** Emitted with the plugin id when the admin opens its details page. */
  details: [id: string]
}>()
</script>

<template>
  <UiCard padding="md" class="h-full" :data-testid="plugin ? `plugin-card-${plugin.id}` : 'plugin-card-loading'" :aria-busy="loading || undefined">
    <UiStack gap="md" class="h-full">
      <!-- Header: the logo tile, the name with its version, and the state at the right end -->
      <UiStack direction="row" align="start" justify="between" gap="md">
        <UiStack direction="row" align="center" gap="md" class="min-w-0">
          <UiPluginAvatar v-if="plugin" :name="plugin.name" :icon-url="plugin.iconUrl" :color="plugin.color" :size="UiSize.Lg" />
          <UiSkeletonBar v-else class="h-12 w-12 rounded-control" aria-hidden="true" />
          <UiStack v-if="plugin" gap="none" class="min-w-0">
            <UiText tone="primary" size="lg" class="truncate font-semibold">{{ plugin.name }}</UiText>
            <UiText tone="muted" size="xs" class="font-mono">v{{ plugin.version }}</UiText>
          </UiStack>
          <UiStack v-else gap="xs" aria-hidden="true">
            <UiSkeletonBar class="h-5 w-32 rounded-md" />
            <UiSkeletonBar class="h-3 w-12 rounded-md" />
          </UiStack>
        </UiStack>
        <UiSkeletonBar v-if="!plugin" class="h-5 w-16 rounded-full" aria-hidden="true" />
        <UiBadge v-else-if="refused" :tone="UiTone.Danger" dot small :data-testid="`plugin-refused-${plugin.id}`">
          {{ t('plugins.pluginCard.refused') }}
        </UiBadge>
        <UiBadge v-else :tone="plugin.isLoaded ? UiTone.Success : UiTone.Warning" dot small>
          {{ plugin.isLoaded ? t('shared.active') : t('plugins.pluginCard.pendingRestart') }}
        </UiBadge>
      </UiStack>

      <UiStack gap="xs" class="min-w-0 flex-1">
        <template v-if="plugin">
          <UiText tone="muted" size="sm" class="line-clamp-2">{{ plugin.description }}</UiText>
          <UiStack direction="row" align="center" gap="xs" wrap>
            <UiBadge
              v-if="plugin.stagedVersion !== null"
              :tone="UiTone.Info"
              small
              :data-testid="`plugin-staged-${plugin.id}`"
            >
              {{ t('plugins.pluginCard.staged', { version: plugin.stagedVersion }) }}
            </UiBadge>
            <UiBadge
              v-if="updateVersion"
              :tone="UiTone.Warning"
              small
              :data-testid="`plugin-update-${plugin.id}`"
            >
              {{ t('plugins.pluginCard.updateAvailable', { version: updateVersion }) }}
            </UiBadge>
            <UiBadge v-if="plugin.isBuiltIn" :tone="UiTone.Info" small :data-testid="`plugin-builtin-${plugin.id}`">
              {{ t('plugins.pluginCard.builtIn') }}
            </UiBadge>
            <UiBadge
              v-for="capability in plugin.capabilities"
              :key="capability"
              :tone="UiTone.Neutral"
              small
              :data-testid="`plugin-capability-${plugin.id}-${capability}`"
            >
              {{ t(`plugins.pluginCard.capabilities.${capability}`) }}
            </UiBadge>
          </UiStack>
          <UiStack v-if="refused" gap="xs" :data-testid="`plugin-refusal-${plugin.id}`">
            <UiText tone="danger" size="sm">{{ reason }}</UiText>
            <UiText tone="muted" size="xs" class="font-mono">
              {{ t('plugins.pluginCard.brokenRule', { id: plugin.id, rule: plugin.refusedRule }) }}
            </UiText>
            <UiText v-if="plugin.refusedDetail" tone="muted" size="xs" class="font-mono break-all">
              {{ plugin.refusedDetail }}
            </UiText>
          </UiStack>
          <UiStack v-if="!refused" gap="xs">
            <UiStack direction="row" align="center" gap="sm" wrap>
              <UiButton
                type="button"
                :variant="UiButtonVariant.Link"
                size="sm"
                :aria-expanded="notesOpen"
                :data-testid="`plugin-notes-toggle-${plugin.id}`"
                @click="notesOpen = !notesOpen"
              >
                {{ notesOpen ? t('plugins.pluginCard.hideNotes') : t('plugins.pluginCard.showNotes', { version: plugin.version }) }}
              </UiButton>
              <UiButton
                v-if="updateVersion"
                type="button"
                :variant="UiButtonVariant.Link"
                size="sm"
                :aria-expanded="updateNotesOpen"
                :data-testid="`plugin-update-notes-toggle-${plugin.id}`"
                @click="updateNotesOpen = !updateNotesOpen"
              >
                {{ updateNotesOpen ? t('plugins.pluginCard.hideUpdateNotes') : t('plugins.pluginCard.showUpdateNotes', { version: updateVersion }) }}
              </UiButton>
              <!-- The details page explains the whole update, with the notes of every version in between. -->
              <UiButton
                v-if="updateVersion && hasDetails"
                type="button"
                :variant="UiButtonVariant.Link"
                size="sm"
                :data-testid="`plugin-changes-${plugin.id}`"
                @click="emit('details', pluginId)"
              >
                {{ t('plugins.pluginCard.whatChanged', { from: plugin.version, to: updateVersion }) }}
              </UiButton>
            </UiStack>
            <UiCollapse :open="notesOpen">
              <UiMarkdown
                :source="plugin.changelog ?? null"
                :empty="t('plugins.pluginCard.noNotes')"
                :data-testid="`plugin-notes-${plugin.id}`"
              />
            </UiCollapse>
            <UiCollapse :open="updateNotesOpen && !!updateVersion">
              <UiMarkdown
                :source="updateChangelog"
                :empty="t('plugins.pluginCard.noNotes')"
                :data-testid="`plugin-update-notes-${plugin.id}`"
              />
            </UiCollapse>
          </UiStack>
          <UiText tone="muted" size="xs">
            {{ t('plugins.pluginCard.categoryByAuthor', { category: plugin.category, author: plugin.author }) }}
          </UiText>
        </template>
        <UiStack v-else gap="xs" aria-hidden="true">
          <UiSkeletonBar class="h-4 w-full rounded-md" />
          <UiSkeletonBar class="h-4 w-2/3 rounded-md" />
          <UiSkeletonBar class="h-3 w-40 rounded-md" />
        </UiStack>
      </UiStack>

      <!-- Footer: the actions under a divider -->
      <UiStack direction="row" align="center" justify="end" gap="sm" wrap class="border-t border-border pt-3">
      <UiButton
        v-if="hasDetails"
        type="button"
        :variant="UiButtonVariant.Secondary"
        size="sm"
        :data-testid="`plugin-details-${pluginId}`"
        @click="emit('details', pluginId)"
      >
        {{ t('plugins.pluginCard.details') }}
      </UiButton>

      <UiButton
        v-if="updateVersion"
        type="button"
        :variant="UiButtonVariant.Primary"
        size="sm"
        :loading="updating"
        :disabled="removing"
        :data-testid="`plugin-update-button-${pluginId}`"
        @click="emit('update', pluginId, updateVersion)"
      >
        {{ t('plugins.pluginRegistry.update', { version: updateVersion }) }}
      </UiButton>

      <UiButton
        v-if="!plugin?.isBuiltIn"
        type="button"
        :variant="UiButtonVariant.Danger"
        size="sm"
        :loading="removing"
        :disabled="loading || updating"
        :data-testid="`plugin-remove-${pluginId}`"
        @click="emit('remove', pluginId)"
      >
        {{ t('plugins.pluginCard.remove') }}
      </UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
