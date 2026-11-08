<script setup lang="ts">
/**
 * The "Registration with the registry" card of the Browse tab.
 *
 * Some registry plugins are given only to an installation the registry knows. This server registers itself and proves it
 * controls its public address on its own (a job at start, and when such a plugin is asked for); this card only reports where
 * that stands: not registered yet, waiting for confirmation, verified, revoked or failed, with the server's sentence for the
 * last failure in the admin's language. A Retry button runs the registration again at once. Nothing secret reaches the
 * panel: the state carries the instance id and the public address, never a key.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTime } from '@/lib/format'
import { RegistryInstanceState } from '@/types/plugin'
import type { RegistryInstanceDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginRegistryInstanceCard. */
const props = defineProps<{
  /** Where this server stands with the registry; null until the first load. */
  instance: RegistryInstanceDto | null
  /** True while the state loads or a retry runs; the button shows progress. */
  busy: boolean
  /** Sentence for the last refused load or retry, null when none. */
  error: string | null
}>()

/** Events of PluginRegistryInstanceCard. */
const emit = defineEmits<{
  /** The admin asked to register again at once. */
  retry: []
}>()

/** The state on show: a server that has not answered yet reads as not registered. */
const state = computed<RegistryInstanceState>(() => props.instance?.state ?? RegistryInstanceState.NotRegistered)

/** Badge colour of each state. */
const stateTones: Record<RegistryInstanceState, UiTone> = {
  [RegistryInstanceState.NotRegistered]: UiTone.Neutral,
  [RegistryInstanceState.Pending]: UiTone.Info,
  [RegistryInstanceState.Verified]: UiTone.Success,
  [RegistryInstanceState.Revoked]: UiTone.Danger,
  [RegistryInstanceState.Error]: UiTone.Warning,
}

/** Whether the Retry button is offered: a verified server has nothing to retry. */
const canRetry = computed<boolean>(() => state.value !== RegistryInstanceState.Verified)

/** The sentence under the state: the server's words for the last failure, or the refusal of the call itself. */
const problem = computed<string | null>(() => props.error ?? props.instance?.message ?? null)
</script>

<template>
  <UiCard padding="md" data-testid="plugin-registry-instance">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('plugins.registryInstance.title') }}</UiHeading>
      <UiText tone="secondary" size="md">{{ t('plugins.registryInstance.description') }}</UiText>

      <UiStack direction="row" align="center" wrap>
        <UiText tone="primary" size="md">{{ t('plugins.registryInstance.stateLabel') }}</UiText>
        <UiBadge :tone="stateTones[state]" data-testid="plugin-registry-instance-state">
          {{ t(`plugins.registryInstance.states.${state}`) }}
        </UiBadge>
        <UiButton
          v-if="canRetry"
          :variant="UiButtonVariant.Secondary"
          size="sm"
          :loading="busy"
          data-testid="plugin-registry-instance-retry"
          @click="emit('retry')"
        >
          {{ t('plugins.registryInstance.retry') }}
        </UiButton>
      </UiStack>

      <UiText v-if="instance?.verifiedAt" tone="muted" size="sm" data-testid="plugin-registry-instance-verified-at">
        {{ t('plugins.registryInstance.verifiedAt', { date: formatDateTime(instance.verifiedAt) }) }}
      </UiText>
      <UiText v-if="instance?.baseUrl" tone="muted" size="sm" data-testid="plugin-registry-instance-address">
        {{ t('plugins.registryInstance.address', { address: instance.baseUrl }) }}
      </UiText>

      <UiAlert v-if="problem" variant="warning" data-testid="plugin-registry-instance-problem">
        {{ problem }}
      </UiAlert>
      <UiText v-if="instance?.nextAttemptAt && state === RegistryInstanceState.Error" tone="muted" size="sm" data-testid="plugin-registry-instance-next">
        {{ t('plugins.registryInstance.nextAttempt', { date: formatDateTime(instance.nextAttemptAt) }) }}
      </UiText>
    </UiStack>
  </UiCard>
</template>
