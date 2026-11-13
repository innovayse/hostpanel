<script setup lang="ts">
/**
 * Glass head of the plugin details page: the logo tile, name, publisher, the Verified and price badges, the
 * state on this server (installed, staged for the restart, update available), the description and
 * the two plugin-wide actions: update to the newest version and uninstall. The component owns no
 * data; it reports what the operator asked for and the page decides. Without details (still being read) it draws the
 * same card with bars in place of the logo, name, badges and description.
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
import type { RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginDetailsHeader. */
const props = defineProps<{
  /** The plugin's registry details; null while they are being read. */
  details: RegistryPluginDetailsDto | null
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
const canUpdate = computed<boolean>(() => props.details !== null && props.details.installed && props.details.updateAvailable)

/** Whether the update button is offered: an update exists and the newest version is not already staged for the restart. */
const canOfferUpdate = computed<boolean>(() => canUpdate.value && props.details?.stagedVersion !== props.details?.latestVersion)
</script>

<template>
  <UiCard padding="md" :aria-busy="details === null ? 'true' : undefined" :data-testid="details === null ? 'plugin-details-loading' : 'plugin-details-header'">
    <UiStack direction="row" align="start" gap="md" reflow-below-md>
      <UiSkeletonBar v-if="details === null" class="h-14 w-14 shrink-0 rounded-card" aria-hidden="true" />
      <UiPluginAvatar v-else :name="details.name" :icon-url="details.iconUrl" :size="UiSize.Lg" data-testid="plugin-details-avatar" />
      <UiStack v-if="details === null" gap="sm" class="min-w-0 flex-1" aria-hidden="true">
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
          <UiBadge v-if="details.installed" :tone="UiTone.Info" dot small data-testid="plugin-details-installed">
            {{ t('pluginDetails.installedVersion', { version: details.installedVersion ?? '' }) }}
          </UiBadge>
          <UiBadge v-else :tone="UiTone.Neutral" dot small data-testid="plugin-details-not-installed">
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
