<script setup lang="ts">
/**
 * Glass head of the plugin details page: the logo tile, name, author, the state on this server (installed
 * version, staged for the restart, update available), the description and the two plugin-wide actions: update to the
 * newest version and uninstall. The installed plugin is the source of truth; the registry's details only add the Verified and price
 * badges, the update offer and the update action, and are absent when the registry is off or does not know the plugin.
 * A built-in plugin has no Uninstall. The component owns no data; it reports what the operator asked for and the page
 * decides. Without the plugin (still being read) it draws the same card with bars in place of the logo, name, badges and description.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { PluginPricing } from '@/types/plugin'
import type { PluginListItemDto, RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginDetailsHeader. */
const props = defineProps<{
  /** The installed plugin as this server lists it; null while the list is being read. */
  plugin: PluginListItemDto | null
  /** The plugin's registry details; null while they are being read, when the registry is off or does not know the plugin. */
  registry: RegistryPluginDetailsDto | null
  /** True while an install, update or uninstall of this plugin is in flight. */
  busy: boolean
}>()

/** Events emitted by PluginDetailsHeader. */
const emit = defineEmits<{
  /** Emitted when the operator asks to update to the newest version. */
  update: []
  /** Emitted when the operator asks to uninstall the plugin. */
  uninstall: []
}>()

/** Badge tone per price. */
const pricingTone: Record<PluginPricing, UiTone> = {
  [PluginPricing.Free]: UiTone.Neutral,
  [PluginPricing.Paid]: UiTone.Warning,
  [PluginPricing.Private]: UiTone.Info,
}

/** Whether the registry offers a newer version than the installed one. */
const canUpdate = computed<boolean>(() => props.registry !== null && props.registry.installed && props.registry.updateAvailable)

/** Whether the update button is offered: an update exists and the newest version is not already staged for the restart. */
const canOfferUpdate = computed<boolean>(() => canUpdate.value && props.plugin?.stagedVersion !== props.registry?.latestVersion)

/** Whether Uninstall is offered: a plugin that ships with the server build cannot be removed. */
const canUninstall = computed<boolean>(() => props.plugin !== null && !props.plugin.isBuiltIn)
</script>

<template>
  <UiCard padding="md" :aria-busy="plugin === null ? 'true' : undefined" :data-testid="plugin === null ? 'plugin-details-loading' : 'plugin-details-header'">
    <UiStack direction="row" align="start" gap="md" reflow-below-md>
      <UiSkeletonBar v-if="plugin === null" class="h-14 w-14 shrink-0 rounded-card" aria-hidden="true" />
      <UiPluginAvatar v-else :name="plugin.name" :icon-url="plugin.iconUrl" :color="plugin.color" :size="UiSize.Lg" data-testid="plugin-details-avatar" />
      <UiStack v-if="plugin === null" gap="sm" class="min-w-0 flex-1" aria-hidden="true">
        <UiStack direction="row" align="center" gap="sm">
          <UiSkeletonBar class="h-5 w-40 rounded-md" />
          <UiSkeletonBar class="h-5 w-16 rounded-full" />
        </UiStack>
        <UiSkeletonBar class="h-3.5 w-32 rounded-md" />
        <UiSkeletonBar class="h-5 w-28 rounded-full" />
        <UiSkeletonBar class="h-3.5 w-full rounded-md" />
        <UiSkeletonBar class="h-3.5 w-2/3 rounded-md" />
      </UiStack>
      <UiStack v-else gap="sm" class="min-w-0 flex-1">
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiText tone="primary" size="lg" class="font-semibold" data-testid="plugin-details-name">{{ plugin.name }}</UiText>
          <UiBadge v-if="registry !== null && registry.verified" :tone="UiTone.Success" small data-testid="plugin-details-verified">
            {{ t('pluginDetails.verified') }}
          </UiBadge>
          <UiBadge v-if="registry !== null" :tone="pricingTone[registry.pricing]" small data-testid="plugin-details-pricing">
            {{ t(`pluginDetails.pricing.${registry.pricing}`) }}
          </UiBadge>
        </UiStack>
        <UiText tone="muted" size="sm" data-testid="plugin-details-author">
          {{ t('pluginDetails.byAuthor', { author: plugin.author }) }}
        </UiText>
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiBadge :tone="UiTone.Info" dot small data-testid="plugin-details-installed">
            {{ t('pluginDetails.installedVersion', { version: plugin.version }) }}
          </UiBadge>
          <UiBadge v-if="plugin.stagedVersion !== null" :tone="UiTone.Warning" small data-testid="plugin-details-staged">
            {{ t('pluginDetails.staged', { version: plugin.stagedVersion }) }}
          </UiBadge>
          <UiBadge v-if="registry !== null && canUpdate" :tone="UiTone.Success" small data-testid="plugin-details-update-available">
            {{ t('pluginDetails.updateAvailable', { version: registry.latestVersion }) }}
          </UiBadge>
        </UiStack>
        <UiText tone="secondary" size="sm" data-testid="plugin-details-description">{{ plugin.description }}</UiText>
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiButton
            v-if="registry !== null && canOfferUpdate"
            type="button"
            :variant="UiButtonVariant.Primary"
            size="sm"
            :loading="busy"
            :disabled="busy"
            data-testid="plugin-details-update"
            @click="emit('update')"
          >
            {{ t('pluginDetails.updateTo', { version: registry.latestVersion }) }}
          </UiButton>
          <UiButton
            v-if="canUninstall"
            type="button"
            :variant="UiButtonVariant.Danger"
            size="sm"
            :disabled="busy"
            data-testid="plugin-details-uninstall"
            @click="emit('uninstall')"
          >
            {{ t('pluginDetails.uninstall') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
