<script setup lang="ts">
/**
 * Full CWP integration configuration page.
 *
 * Renders CwpBrandHeader, IntegrationConfigForm, and CwpStatusSidebar in a two-column layout.
 * Fetches config and server-info in parallel on mount.
 */
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useIntegrationsStore } from '../stores/integrationsStore'
import { useCwpServerInfo } from '../composables/useCwpServerInfo'
import CwpBrandHeader from '../components/CwpBrandHeader.vue'
import CwpStatusSidebar from '../components/CwpStatusSidebar.vue'
import IntegrationConfigForm from '../components/IntegrationConfigForm.vue'
import type { IntegrationConfigPayload } from '../types/integration.types'

const store = useIntegrationsStore()
const { info, fetch: fetchInfo } = useCwpServerInfo()

/** True while the test connection request is in progress. */
const testing = ref(false)

/** Error message from failed test, shown under the form. */
const testError = ref<string | null>(null)

/** True while the config save is in progress. */
const saving = ref(false)

/** Derived header status from server-info. */
const headerStatus = computed((): 'connected' | 'error' | 'unknown' => {
  if (!info.value) return 'unknown'
  return info.value.connected ? 'connected' : 'error'
})

onMounted(async () => {
  await Promise.all([
    store.fetchOne('cwp'),
    fetchInfo(),
  ])
})

/**
 * Saves the current config to the backend, then re-fetches server-info.
 *
 * @param payload - Updated enabled state and config values from the form.
 * @returns Promise resolving when save and re-fetch complete.
 */
async function handleSave(payload: IntegrationConfigPayload): Promise<void> {
  saving.value = true
  try {
    await store.saveConfig('cwp', payload)
    await fetchInfo()
  } finally {
    saving.value = false
  }
}

/**
 * Tests the CWP connection and updates the sidebar status.
 *
 * @returns Promise resolving when test completes.
 */
async function handleTest(): Promise<void> {
  testing.value = true
  testError.value = null
  try {
    await store.testConnection('cwp')
    if (store.testResult && !store.testResult.success) {
      testError.value = store.testResult.message
    }
    await fetchInfo()
  } finally {
    testing.value = false
  }
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-5xl w-full flex flex-col gap-5">

    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-[0.78rem]">
      <RouterLink
        to="/integrations"
        class="text-text-secondary hover:text-primary-400 transition-colors no-underline"
      >
        Integrations
      </RouterLink>
      <svg class="w-3.5 h-3.5 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="9 18 15 12 9 6"/>
      </svg>
      <span class="text-text-primary font-medium">CentOS Web Panel</span>
    </div>

    <!-- Branded header -->
    <CwpBrandHeader
      :version="info?.cwpVersion ?? null"
      :status="headerStatus"
    />

    <!-- Two-column layout -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">

      <!-- Config form — 2/3 width -->
      <div class="lg:col-span-2 flex flex-col gap-4">
        <IntegrationConfigForm
          v-if="store.current"
          :integration="store.current"
          :loading="saving || store.loading"
          @save="handleSave"
          @test="handleTest"
        />

        <!-- Test error banner -->
        <div
          v-if="testError"
          class="flex items-center gap-2 text-[0.82rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-xl px-4 py-3"
        >
          <svg class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          Could not connect: {{ testError }}
        </div>
      </div>

      <!-- Status sidebar — 1/3 width -->
      <div class="lg:col-span-1">
        <CwpStatusSidebar
          :info="info"
          :testing="testing"
          @test="handleTest"
        />
      </div>

    </div>
  </div>
</template>
