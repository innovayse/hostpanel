<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + 2/3 config form + 1/3 status sidebar.
 * Special case: if slug is "cwp", renders CwpIntegrationPage instead.
 */
import { onMounted, defineAsyncComponent } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useIntegrationsStore } from '../stores/integrationsStore'
import IntegrationConfigForm from '../components/IntegrationConfigForm.vue'
import IntegrationStatusSidebar from '../components/IntegrationStatusSidebar.vue'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationConfigPayload, IntegrationSlug } from '../types/integration.types'

const route = useRoute()
const router = useRouter()
const store = useIntegrationsStore()

/** Slug from the current route (e.g. "stripe"). */
const slug = route.params.slug as string

/** True when the current integration is the CWP plugin. */
const isCwp = slug === 'cwp'

/** Async CWP integration page — only loaded when slug is "cwp". */
const CwpIntegrationPage = defineAsyncComponent(
  () => import('./CwpIntegrationPage.vue'),
)

onMounted(() => {
  if (isCwp) {
    const storeRef = useIntegrationsStore()
    if (storeRef.integrations.length > 0 && !storeRef.integrations.some(i => i.slug === 'cwp')) {
      router.replace({ path: '/plugins' })
    }
    return
  }
  store.fetchOne(slug)
})

/** Static metadata hint for this integration. */
const hint = INTEGRATION_META[slug as IntegrationSlug]?.hint

/**
 * Handles save event from IntegrationConfigForm.
 *
 * @param payload - Updated config and enabled state.
 * @returns Promise that resolves when save is complete.
 */
async function handleSave(payload: IntegrationConfigPayload): Promise<void> {
  await store.saveConfig(slug, payload)
}

/**
 * Handles test event from IntegrationConfigForm.
 *
 * @returns Promise that resolves when test is complete.
 */
async function handleTest(): Promise<void> {
  await store.testConnection(slug)
}
</script>

<template>
  <CwpIntegrationPage v-if="isCwp" />

  <div v-else class="p-4 sm:p-6 lg:p-8 max-w-5xl w-full">

    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-[0.78rem] mb-6">
      <RouterLink
        to="/integrations"
        class="text-text-secondary hover:text-primary-400 transition-colors no-underline"
      >
        Integrations
      </RouterLink>
      <svg class="w-3.5 h-3.5 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="9 18 15 12 9 6"/>
      </svg>
      <span class="text-text-primary font-medium">{{ store.current?.name ?? slug }}</span>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.current" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading…
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4">
      {{ store.error }}
    </div>

    <!-- Content -->
    <div v-else-if="store.current" class="grid grid-cols-1 lg:grid-cols-3 gap-5">
      <div class="lg:col-span-2">
        <IntegrationConfigForm
          :integration="store.current"
          :loading="store.loading"
          @save="handleSave"
          @test="handleTest"
        />
      </div>
      <div class="lg:col-span-1">
        <IntegrationStatusSidebar
          :last-tested-at="store.current.lastTestedAt"
          :test-result="store.testResult"
          :hint="hint"
        />
      </div>
    </div>

  </div>
</template>
