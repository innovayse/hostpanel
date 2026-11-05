<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + 2/3 config form + 1/3 status sidebar.
 * Special cases: slug "cwp" renders CwpIntegrationView and "cwp7" renders Cwp7IntegrationView.
 * A save reports its outcome (success, or the API's sentence) above the form; a refused load shows
 * the sentence with a Retry.
 */
import { computed, defineAsyncComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useIntegrationsStore } from '@/stores/integrations'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiTone } from '@/utils/ui/tone'
import { INTEGRATION_META } from '@/types/integrationmeta'
import { IntegrationSlug } from '@/types/integration'
import type { IntegrationConfigPayload } from '@/types/integration'
import IntegrationBreadcrumb from '@/components/integrations/IntegrationBreadcrumb.vue'
import IntegrationConfigForm from '@/components/integrations/IntegrationConfigForm.vue'
import IntegrationStatusSidebar from '@/components/integrations/IntegrationStatusSidebar.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Current route, whose param is the slug. */
const route = useRoute()

/** Router, to leave the CWP page when the plugin is not installed. */
const router = useRouter()

/** Integrations store: the detail, the test state and the refusal sentence. */
const store = useIntegrationsStore()

/** Slug from the current route. */
const slug = route.params.slug as string

/** True when the current integration is the CWP plugin. */
const isCwp = slug === IntegrationSlug.Cwp

/** True when the current integration is CWP7. */
const isCwp7 = slug === IntegrationSlug.Cwp7

/** Async CWP integration page: only loaded when slug is "cwp". */
const CwpIntegrationView = defineAsyncComponent(
  () => import('@/views/CwpIntegrationView.vue'),
)

/** Async CWP7 integration page: only loaded when slug is "cwp7". */
const Cwp7IntegrationView = defineAsyncComponent(
  () => import('@/views/Cwp7IntegrationView.vue'),
)

/** The sentence of a refused load of the integration, shown with Retry; null when it loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last save. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Loads the integration and records whether the load was refused.
 *
 * @returns Resolves when the integration has settled.
 */
const load = async (): Promise<void> => {
  await store.fetchOne(slug)
  loadError.value = store.error
}

/** Static metadata hint for this integration. */
const hint = INTEGRATION_META[slug as IntegrationSlug]?.hint

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
 * Handles save event from IntegrationConfigForm.
 *
 * @param payload - Updated config and enabled state.
 * @returns Promise that resolves when save is complete.
 */
const handleSave = async (payload: IntegrationConfigPayload): Promise<void> => {
  feedback.clear()
  if (await store.saveConfig(slug, payload)) {
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
  await store.testConnection(slug)
}

/** Loads the integration, or leaves for the plugins page when the CWP plugin is not installed. */
onMounted(() => {
  if (isCwp) {
    if (store.integrations.length > 0 && !store.integrations.some(i => i.slug === IntegrationSlug.Cwp)) {
      void router.replace({ path: AppRoute.Plugins })
    }
    return
  }
  if (isCwp7) {
    return
  }
  void load()
})
</script>

<template>
  <CwpIntegrationView v-if="isCwp" />
  <Cwp7IntegrationView v-else-if="isCwp7" />

  <UiStack v-else gap="md" class="w-full">
    <IntegrationBreadcrumb
      :name="store.current?.name ?? slug"
      :is-plugin="store.current?.isPlugin ?? false"
      :icon-url="store.current?.iconUrl ?? null"
    />

    <!-- Loading -->
    <UiSkeleton v-if="store.loading && !store.current" :lines="4" data-testid="integration-loading" />

    <!-- Error -->
    <UiErrorState
      v-else-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="integration-error"
      @retry="load"
    />

    <!-- Outcome of the last save -->
    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="integration-feedback" />

    <!-- Content -->
    <UiSplit v-if="store.current && loadError === null">
      <IntegrationConfigForm
        :integration="store.current"
        :loading="store.loading"
        :testing="store.testing"
        @save="handleSave"
        @test="handleTest"
      />
      <IntegrationStatusSidebar
        :test-result="store.testResult"
        :testing="store.testing"
        :connection="store.current.connection"
        :endpoint-host="endpointHost"
        :hint="hint"
      />
    </UiSplit>
  </UiStack>
</template>
