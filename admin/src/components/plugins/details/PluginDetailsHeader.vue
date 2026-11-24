<script setup lang="ts">
/**
 * Glass head of the plugin details page: the logo tile, author, the state on this server (installed
 * version, staged for the restart, update available), the description and the two plugin-wide actions: update to the
 * newest version and uninstall. The installed plugin is the source of truth; the registry's details only add the Verified and price
 * badges, the update offer and the update action, and are absent when the registry is off or does not know the plugin.
 * A built-in plugin has no Uninstall. The component owns no data; it reports what the operator asked for and the page
 * decides. While the list is read the page draws the head card of `PluginDetailsView.schema` instead, so this component only
 * exists for a plugin the server lists. The plugin name is the page title of the view (its `UiPageHeader`), so it is not repeated here.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { PluginPricing } from '@/types/plugin'
import type { PluginListItemDto, RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginDetailsHeader. */
const props = defineProps<{
  /** The installed plugin as this server lists it. */
  plugin: PluginListItemDto
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
const canOfferUpdate = computed<boolean>(() => canUpdate.value && props.plugin.stagedVersion !== props.registry?.latestVersion)

/** Whether Uninstall is offered: a plugin that ships with the server build cannot be removed. */
const canUninstall = computed<boolean>(() => !props.plugin.isBuiltIn)
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-details-header">
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Start" :gap="UiGap.Md" reflow-below-md>
      <UiPluginAvatar :name="plugin.name" :icon-url="plugin.iconUrl" :color="plugin.color" :size="UiSize.Lg" data-testid="plugin-details-avatar" />
      <UiStack :gap="UiGap.Sm" class="min-w-0 flex-1">
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
          <UiBadge v-if="registry !== null && registry.verified" :tone="UiTone.Success" small data-testid="plugin-details-verified">
            {{ t('pluginDetails.verified') }}
          </UiBadge>
          <UiBadge v-if="registry !== null" :tone="pricingTone[registry.pricing]" small data-testid="plugin-details-pricing">
            {{ t(`pluginDetails.pricing.${registry.pricing}`) }}
          </UiBadge>
        </UiStack>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-details-author">
          {{ t('pluginDetails.byAuthor', { author: plugin.author }) }}
        </UiText>
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
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
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" data-testid="plugin-details-description">{{ plugin.description }}</UiText>
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
          <UiButton :icon="Icon.Download"
            v-if="registry !== null && canOfferUpdate"
            :type="UiButtonType.Button"
            :variant="UiButtonVariant.Primary"
            :size="UiSize.Sm"
            :loading="busy"
            :disabled="busy"
            data-testid="plugin-details-update"
            @click="emit('update')"
          >
            {{ t('pluginDetails.updateTo', { version: registry.latestVersion }) }}
          </UiButton>
          <UiButton :action="UiActionKind.Delete"
            v-if="canUninstall"
            :type="UiButtonType.Button"
            :variant="UiButtonVariant.Danger"
            :size="UiSize.Sm"
            :disabled="busy"
            data-testid="plugin-details-uninstall"
            @click="emit('uninstall')"
          >
            {{ t('shared.uninstall') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
