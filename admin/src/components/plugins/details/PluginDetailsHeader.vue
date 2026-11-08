<script setup lang="ts">
/**
 * Head of the plugin details page: the picture, name, publisher, the Verified and price badges, the
 * state on this server (installed, staged for the restart, update available), the description and
 * the two plugin-wide actions: update to the newest version and uninstall. The component owns no
 * data; it reports what the operator asked for and the page decides.
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
import type { RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginDetailsHeader. */
const props = defineProps<{
  /** The plugin's registry details. */
  details: RegistryPluginDetailsDto
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

/** Whether the plugin is installed with a newer version on offer. */
const canUpdate = computed<boolean>(() => props.details.installed && props.details.updateAvailable)

/** Whether the update button is offered: an update exists and the newest version is not already staged for the restart. */
const canOfferUpdate = computed<boolean>(() => canUpdate.value && props.details.stagedVersion !== props.details.latestVersion)
</script>

<template>
  <UiCard padding="md" data-testid="plugin-details-header">
    <UiStack direction="row" align="start" gap="md" reflow-below-md>
      <UiPluginAvatar :name="details.name" :icon-url="details.iconUrl" data-testid="plugin-details-avatar" />
      <UiStack gap="sm" class="min-w-0 flex-1">
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiText tone="primary" size="lg" class="font-semibold" data-testid="plugin-details-name">{{ details.name }}</UiText>
          <UiBadge v-if="details.verified" :tone="UiTone.Success" small data-testid="plugin-details-verified">
            {{ t('pluginDetails.verified') }}
          </UiBadge>
          <UiBadge :tone="pricingTone[details.pricing]" small data-testid="plugin-details-pricing">
            {{ t(`pluginDetails.pricing.${details.pricing}`) }}
          </UiBadge>
        </UiStack>
        <UiText tone="muted" size="sm" data-testid="plugin-details-author">
          {{ t('pluginDetails.byAuthor', { author: details.author }) }}
        </UiText>
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiBadge v-if="details.installed" :tone="UiTone.Info" small data-testid="plugin-details-installed">
            {{ t('pluginDetails.installedVersion', { version: details.installedVersion ?? '' }) }}
          </UiBadge>
          <UiBadge v-else :tone="UiTone.Neutral" small data-testid="plugin-details-not-installed">
            {{ t('pluginDetails.notInstalled') }}
          </UiBadge>
          <UiBadge v-if="details.stagedVersion !== null" :tone="UiTone.Warning" small data-testid="plugin-details-staged">
            {{ t('pluginDetails.staged', { version: details.stagedVersion }) }}
          </UiBadge>
          <UiBadge v-if="canUpdate" :tone="UiTone.Success" small data-testid="plugin-details-update-available">
            {{ t('pluginDetails.updateAvailable', { version: details.latestVersion }) }}
          </UiBadge>
        </UiStack>
        <UiText tone="secondary" size="sm" data-testid="plugin-details-description">{{ details.description }}</UiText>
        <UiStack direction="row" align="center" gap="sm" wrap>
          <UiButton
            v-if="canOfferUpdate"
            type="button"
            :variant="UiButtonVariant.Primary"
            size="sm"
            :loading="busy"
            :disabled="busy"
            data-testid="plugin-details-update"
            @click="emit('update')"
          >
            {{ t('pluginDetails.updateTo', { version: details.latestVersion }) }}
          </UiButton>
          <UiButton
            v-if="details.installed"
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
