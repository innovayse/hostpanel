<script setup lang="ts">
/**
 * Full CWP integration configuration page.
 *
 * Draws the three cards of `CwpIntegrationView.schema` with `UiDetailCards`: the bodies of the brand header, the config
 * form and the provider status are the slots of those cards, and while the config is read the cards are placeholders.
 * Fetches config and server-info in parallel on mount; a refused load shows the API's sentence with a Retry, and a save
 * reports its outcome (success, or the sentence) under the form.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useIntegrationsStore } from '@/stores/integrations'
import { useResultBanner } from '@/composables/useResultBanner'
import { IntegrationSlug } from '@/types/integration'
import type { IntegrationConfigPayload } from '@/types/integration'
import { ProviderConnectionState } from '@/types/providerstatus'
import type { ProviderMetric } from '@/types/providerstatus'
import CwpBrandHeader from '@/components/integrations/CwpBrandHeader.vue'
import IntegrationConfigForm from '@/components/integrations/IntegrationConfigForm.vue'
import ProviderStatusCard from '@/components/integrations/ProviderStatusCard.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiTone } from '@/utils/ui/tone'
import schema from '@/views/CwpIntegrationView.schema'

/** Translation function. */
const { t } = useI18n()

/** Integrations store: the CWP config, the live server status, the test state and the refusal sentence. */
const store = useIntegrationsStore()

/** True while the test connection request is in progress. */
const testing = ref(false)

/** Refusal reported by a failed test, shown under the form. */
const testError = ref<string | null>(null)

/** True while the config save is in progress. */
const saving = ref(false)

/** The sentence of a refused load of the config, shown with Retry; null when it loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last save. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the config and the live server info in parallel and records whether the config was refused.
 *
 * @returns Resolves when both have settled.
 */
const load = async (): Promise<void> => {
  await Promise.all([
    store.fetchOne(IntegrationSlug.Cwp),
    store.fetchCwpServerInfo(),
  ])
  loadError.value = store.error
}

/** Derived header status from server-info. */
const headerStatus = computed(() => {
  if (!store.cwpServerInfo) {
    return ProviderConnectionState.Unknown
  }
  return store.cwpServerInfo.connected ? ProviderConnectionState.Connected : ProviderConnectionState.Error
})

/** Figures of the status card: accounts on the server and the CWP version. */
const metrics = computed<ProviderMetric[]>(() => {
  const figures: ProviderMetric[] = [
    { label: t('shared.accounts'), value: store.cwpServerInfo?.accountsCount ?? null, large: true },
  ]
  if (store.cwpServerInfo?.version) {
    figures.push({ label: t('integrations.providerStatus.cwpVersion'), value: store.cwpServerInfo.version, mono: true })
  }
  return figures
})

/**
 * Saves the current config to the backend, then re-fetches server-info.
 *
 * @param payload - Updated enabled state and config values from the form.
 * @returns Promise resolving when save and re-fetch complete.
 */
const handleSave = async (payload: IntegrationConfigPayload): Promise<void> => {
  feedback.clear()
  saving.value = true
  const saved = await store.saveConfig(IntegrationSlug.Cwp, payload)
  if (saved) {
    feedback.show(t('integrations.integrationDetail.saved'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
  await store.fetchCwpServerInfo()
  saving.value = false
}

/**
 * Tests the CWP connection and updates the status card.
 *
 * @returns Promise resolving when test completes.
 */
const handleTest = async (): Promise<void> => {
  testing.value = true
  testError.value = null
  await store.testConnection(IntegrationSlug.Cwp)
  if (store.testResult && !store.testResult.success) {
    testError.value = store.testResult.message
  }
  await store.fetchCwpServerInfo()
  testing.value = false
}

/** Loads the config and the live server info in parallel. */
onMounted(() => {
  void load()
})
</script>

<template>
  <!-- The three cards of the schema: placeholders while the config is read, their bodies after -->
  <UiDetailCards :columns="schema.columns" :ratio="schema.ratio" :loading="loadError === null && store.current === null">
    <template #card-brand>
      <CwpBrandHeader :version="store.cwpServerInfo?.version ?? null" :status="headerStatus" />
    </template>

    <template #card-config>
      <UiStack gap="md">
        <UiErrorState
          v-if="loadError !== null"
          :message="loadError"
          :loading="store.loading"
          testid="cwp-load-error"
          @retry="load"
        />
        <IntegrationConfigForm
          v-else-if="store.current"
          :integration="store.current"
          :loading="saving || store.loading"
          :testing="store.testing"
          @save="handleSave"
          @test="handleTest"
        />

        <!-- The outcome of the last save -->
        <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="cwp-save-feedback" />

        <!-- The refusal the test returned, in the server's words -->
        <UiAlert v-if="testError" variant="error" data-testid="cwp-test-error">
          {{ testError }}
        </UiAlert>
      </UiStack>
    </template>

    <template #card-status>
      <ProviderStatusCard :info="store.cwpServerInfo" :metrics="metrics" :testing="testing" @test="handleTest" />
    </template>
  </UiDetailCards>
</template>
