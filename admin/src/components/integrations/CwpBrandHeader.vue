<script setup lang="ts">
/**
 * Branded header for the CWP integration config page.
 *
 * Shows the CWP logo, title, a version badge, and the connection state as a toned badge.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { INTEGRATION_META } from '@/types/integrationmeta'
import { IntegrationSlug } from '@/types/integration'
import { ProviderConnectionState } from '@/types/providerstatus'
import type { UiTone } from '@/utils/ui/tone'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Translation function. */
const { t } = useI18n()

/** Props for CwpBrandHeader. */
const props = defineProps<{
  /** CWP version string to display in the badge, or null while loading. */
  version: string | null
  /** Connection status: controls the badge tone and caption. */
  status: ProviderConnectionState
}>()

/** Static metadata of the CWP integration (logo and tile colour). */
const meta = INTEGRATION_META[IntegrationSlug.Cwp]

/** Badge tone per connection state. */
const statusTone = computed<UiTone>(() => {
  if (props.status === ProviderConnectionState.Connected) {
    return 'success'
  }
  return props.status === ProviderConnectionState.Error ? 'danger' : 'neutral'
})

/** Badge caption per connection state. */
const statusLabel = computed<string>(() => {
  if (props.status === ProviderConnectionState.Connected) {
    return t('shared.connected')
  }
  return props.status === ProviderConnectionState.Error
    ? t('integrations.cwpBrandHeader.connectionError')
    : t('integrations.cwpBrandHeader.unknown')
})
</script>

<template>
  <UiCard padding="md" data-testid="cwp-brand-header">
    <UiStack direction="row" align="center" gap="md" wrap>
      <UiStack align="center" justify="center" class="h-11 w-11 shrink-0 rounded-control p-2" :class="meta.color">
        <UiImage :src="meta.logo" alt="" class="h-full w-full object-contain brightness-0 invert" />
      </UiStack>
      <UiStack gap="xs" class="min-w-0 flex-1">
        <UiHeading :level="1" variant="card" class="truncate">{{ t('integrations.cwpBrandHeader.centosWebPanel') }}</UiHeading>
        <UiText tone="secondary" size="sm">{{ t('integrations.cwpBrandHeader.provisioningProvider') }}</UiText>
      </UiStack>
      <UiBadge v-if="props.version" tone="neutral" small class="font-mono" data-testid="cwp-version">
        v{{ props.version }}
      </UiBadge>
      <UiBadge :tone="statusTone" dot data-testid="cwp-status">{{ statusLabel }}</UiBadge>
    </UiStack>
  </UiCard>
</template>
