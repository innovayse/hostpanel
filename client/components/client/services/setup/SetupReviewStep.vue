<template>
  <UiCard padding="xl" data-testid="setup-step-review">
    <UiStack align="center" gap="xs" class="text-center mb-8">
      <UiTile :icon="Rocket" tone="primary" size="xl" />
      <UiHeading :level="2" size="subsection">{{ $t('client.setup.readyTitle') }}</UiHeading>
      <UiText tone="muted">{{ $t('client.setup.readyDesc') }}</UiText>
    </UiStack>

    <UiCard padding="sm" class="mb-8">
      <UiStack gap="sm">
        <UiKeyValue :label="$t('client.setup.planLabel')" :value="planName" />
        <UiKeyValue :label="$t('client.setup.domainLabel')" :value="domain" />
        <UiKeyValue :label="$t('client.setup.usernameSummaryLabel')" :value="username" />
      </UiStack>
    </UiCard>

    <UiAlert v-if="error" variant="error" class="mb-6" data-testid="setup-error">{{ error }}</UiAlert>

    <UiStack gap="sm">
      <UiButton full-width size="lg" :loading="submitting" data-testid="setup-build" @click="emit('build')">
        <Zap v-if="!submitting" :size="18" class="mr-2" />
        {{ $t('client.setup.build') }}
      </UiButton>
      <UiButton variant="ghost" full-width :disabled="submitting" data-testid="setup-change" @click="emit('back')">
        {{ $t('client.setup.change') }}
      </UiButton>
    </UiStack>

    <UiHeading :level="3" size="label" class="text-center mt-6">{{ $t('client.setup.provisioningTime') }}</UiHeading>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * SetupReviewStep. The last step of the setup wizard: a recap of the plan, domain and username,
 * the API's refusal when provisioning failed, and the button that starts provisioning.
 */
import { Rocket, Zap } from 'lucide-vue-next'

/** Props of SetupReviewStep. */
interface Props {
  /** Name of the plan being set up. */
  planName: string
  /** The chosen domain. */
  domain: string
  /** The chosen control-panel username. */
  username: string
  /** Whether provisioning is in flight. */
  submitting: boolean
  /** The API's refusal sentence; empty when there is none. */
  error: string
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to start provisioning. */
  build: []
  /** The reader asked to go back and change something. */
  back: []
}>()
</script>
