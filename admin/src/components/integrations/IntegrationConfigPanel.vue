<script setup lang="ts">
/**
 * The config panel of one integration, shared by the integration page and the Settings tab of a plugin.
 *
 * Loads the integration by its slug through the integrations store and draws the config form beside the status
 * sidebar. While the integration loads the same two cards are drawn with bars where their values go; a refused load shows the API's sentence
 * with a Retry; a save reports its outcome (success, or the API's sentence) above the form.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useIntegrationsStore } from '@/stores/integrations'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiTone } from '@/utils/ui/tone'
import { INTEGRATION_META } from '@/types/integrationmeta'
import type { IntegrationConfigPayload, IntegrationSlug } from '@/types/integration'
import IntegrationConfigForm from '@/components/integrations/IntegrationConfigForm.vue'
import IntegrationStatusSidebar from '@/components/integrations/IntegrationStatusSidebar.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'

/** Props for IntegrationConfigPanel. */
const props = defineProps<{
  /** Slug of the integration to configure (a plugin's id for a plugin integration). */
  slug: string
}>()

/** Translation function. */
const { t } = useI18n()

/** Integrations store: the detail, the test state and the refusal sentence. */
const store = useIntegrationsStore()

/** The sentence of a refused load of the integration, shown with Retry; null when it loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last save. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Static metadata hint for this integration. */
const hint = computed<string | undefined>(() => INTEGRATION_META[props.slug as IntegrationSlug]?.hint)

/**
 * Host the integration talks to, for the status card. Only hosted-gateway and server-backed
 * integrations carry a URL field; for the rest this is undefined and the card omits the row.
 * The scheme, path, query and fragment are cut off so a pasted full URL still reads cleanly.
 */
const endpointHost = computed<string | undefined>(() => {
  const cfg = store.current?.config ?? {}
  const raw = cfg.gateway_url?.value || cfg.host?.value || cfg.api_url?.value
  if (!raw) {
    return undefined
  }
  return raw.replace(/^[a-z][a-z0-9+.-]*:\/\//i, '').split(/[/?#]/)[0] || undefined
})

/**
 * Loads the integration and records whether the load was refused.
 *
 * @returns Resolves when the integration has settled.
 */
const load = async (): Promise<void> => {
  feedback.clear()
  await store.fetchOne(props.slug)
  loadError.value = store.error
}

/**
 * Handles save event from IntegrationConfigForm.
 *
 * @param payload - Updated config and enabled state.
 * @returns Promise that resolves when save is complete.
 */
const handleSave = async (payload: IntegrationConfigPayload): Promise<void> => {
  feedback.clear()
  if (await store.saveConfig(props.slug, payload)) {
    feedback.show(t('integrations.integrationDetail.saved'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Handles test event from IntegrationConfigForm.
 *
 * @returns Promise that resolves when test is complete.
 */
const handleTest = async (): Promise<void> => {
  await store.testConnection(props.slug)
}

// The same panel serves every integration: a new slug loads that integration.
watch(() => props.slug, () => {
  void load()
})

// Loads the integration when the panel opens.
onMounted(load)
</script>

<template>
  <UiStack gap="md" class="w-full" data-testid="integration-config-panel">
    <!-- Error -->
    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="integration-error"
      @retry="load"
    />

    <!-- Outcome of the last save -->
    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="integration-feedback" />

    <!-- Content: the same split while the integration is read, with placeholders where its values go -->
    <UiSplit v-if="loadError === null" :aria-busy="store.current === null ? 'true' : undefined">
      <IntegrationConfigForm
        :integration="store.current"
        :loading="store.loading"
        :testing="store.testing"
        @save="handleSave"
        @test="handleTest"
      />
      <IntegrationStatusSidebar
        :loading="store.current === null"
        :test-result="store.testResult"
        :testing="store.testing"
        :connection="store.current?.connection"
        :endpoint-host="endpointHost"
        :hint="hint"
      />
    </UiSplit>
  </UiStack>
</template>
