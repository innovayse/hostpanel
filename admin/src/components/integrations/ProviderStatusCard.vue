<script setup lang="ts">
/**
 * Live status card of a hosting panel provisioning provider.
 *
 * Displays the connection badge, the provider-specific figures the page passes in, the last test
 * time and the refusal the provider reported. Emits `test` when the admin asks for a new test.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatRelativeFromNow } from '@/lib/format'
import type { ProviderInfo, ProviderMetric } from '@/types/providerstatus'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Translation function and the active locale (for time wording). */
const { t, locale } = useI18n()

/** Props for ProviderStatusCard. */
const props = defineProps<{
  /** Server info, null while loading or when the lookup failed. */
  info: ProviderInfo | null
  /** Figures shown under the connection badge (only while connected). */
  metrics: ProviderMetric[]
  /** True while a test connection request is in progress. */
  testing: boolean
}>()

/** Events emitted by ProviderStatusCard. */
const emit = defineEmits<{
  /** Emitted when the user clicks the Test Connection button. */
  test: []
}>()

/** Caption of the connection badge. */
const connectionLabel = computed<string>(() => {
  if (props.info === null) {
    return t('integrations.providerStatus.statusUnknown')
  }
  return props.info.connected ? t('shared.connected') : t('integrations.providerStatus.notConnected')
})

/** Human-readable relative time since the last test. */
const lastTestedLabel = computed<string>(() =>
  props.info?.lastTestedAt
    ? formatRelativeFromNow(props.info.lastTestedAt, locale.value)
    : t('integrations.common.never'),
)
</script>

<template>
  <UiCard padding="md" data-testid="provider-status">
    <UiStack gap="lg">
      <UiBadge :tone="info?.connected ? 'success' : 'neutral'" dot data-testid="provider-status-connection">
        {{ connectionLabel }}
      </UiBadge>

      <template v-if="info?.connected">
        <UiStack v-for="metric in metrics" :key="metric.label" gap="xs">
          <UiText tone="muted" size="xs" class="font-semibold uppercase tracking-[0.08em]">{{ metric.label }}</UiText>
          <UiText
            tone="primary"
            :class="[metric.large ? 'font-display text-3xl font-bold tabular-nums' : 'text-[0.85rem] font-medium', metric.mono ? 'font-mono' : '']"
          >
            {{ metric.value ?? '—' }}
          </UiText>
        </UiStack>
      </template>

      <UiStack gap="xs">
        <UiText tone="muted" size="xs" class="font-semibold uppercase tracking-[0.08em]">
          {{ t('integrations.providerStatus.lastTested') }}
        </UiText>
        <UiText tone="secondary" size="md" data-testid="provider-status-last-tested">{{ lastTestedLabel }}</UiText>
      </UiStack>

      <UiAlert v-if="info && !info.connected && info.errorMessage" variant="error" data-testid="provider-status-error">
        {{ info.errorMessage }}
      </UiAlert>

      <UiButton
        type="button"
        variant="secondary"
        block
        :loading="testing"
        data-testid="provider-status-test"
        @click="emit('test')"
      >
        {{ t('shared.testConnection') }}
      </UiButton>
    </UiStack>
  </UiCard>
</template>
