<template>
  <ClientServicesSetupStepCard :icon="Globe" :title="$t('client.setup.domainTitle')" :subtitle="$t('client.setup.domainSubtitle')" data-testid="setup-step-domain">
    <UiInput
      v-model="domain"
      :label="$t('client.setup.domainName')"
      :placeholder="$t('client.setup.domainPlaceholder')"
      :error="hasPurchasedDomain ? undefined : domainError"
      :disabled="hasPurchasedDomain"
      testid="setup-domain"
      required
    />
    <UiText size="xs" :tone="hasPurchasedDomain ? 'primary' : 'subtle'">
      {{ hasPurchasedDomain ? $t('client.setup.domainHintPurchased') : $t('client.setup.domainHintNew') }}
    </UiText>
    <UiButton full-width size="lg" :disabled="!domain || Boolean(domainError)" data-testid="setup-domain-next" @click="emit('next')">
      {{ $t('client.setup.continue') }}
      <ArrowRight :size="18" class="ml-2" />
    </UiButton>
  </ClientServicesSetupStepCard>
</template>

<script setup lang="ts">
/**
 * SetupDomainStep. The first step of the setup wizard: the domain the hosting is for. A domain
 * bought with the plan is shown locked; otherwise the client types one and is told at once when
 * it is not a valid hostname.
 */
import { ArrowRight, Globe } from 'lucide-vue-next'

/** The domain as typed (v-model). */
const domain = defineModel<string>('domain', { required: true })

/** Props of SetupDomainStep. */
interface Props {
  /** Why the typed domain is not acceptable; empty when it is. */
  domainError: string
  /** Whether the domain was purchased with the plan (and so cannot be edited). */
  hasPurchasedDomain: boolean
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to go to the next step. */
  next: []
}>()
</script>
